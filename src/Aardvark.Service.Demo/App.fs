namespace Aardvark.UI

open System
open System.Text
open System.Collections.Generic


open Suave
open Suave.Http
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Service

type App<'model, 'mmodel, 'msg> =
    {
        view : 'mmodel -> Ui<'msg>
        update : 'model -> 'msg -> 'model
        initial : 'model
    }

type MApp<'model, 'mmodel, 'msg> =
    {
        cview : 'mmodel -> Ui<'msg>
        cupdate : 'model -> 'msg -> 'model
        cinit : 'model -> 'mmodel
        capply : 'mmodel -> 'model -> unit
        cinitial : 'model
    }

[<AutoOpen>]
module ``UI Extensions`` =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Ui =
        let private main =
            String.concat "\r\n" [
                "<html>"
                "   <head>"
                "       <title>Aardvark rocks \\o/</title>"
                "       <script src=\"https://code.jquery.com/jquery-3.1.1.min.js\"></script>"
                "       <script src=\"https://cdnjs.cloudflare.com/ajax/libs/jquery-resize/1.1/jquery.ba-resize.min.js\"></script>"
                "       <script src=\"/aardvark.js\"></script>"
                "       <script>"
                "           var aardvark = {};"
                "           function getUrl(proto, subpath) {"
                "               var l = window.location;"
                "               var path = l.pathname;"
                "               if(l.port === \"\") {"
                "                   return proto + l.hostname + path + subpath;"
                "               }"
                "               else {"
                "                   return proto + l.hostname + ':' + l.port + path + subpath;"
                "               }"
                "           }"
                "           var url = getUrl('ws://', 'events');"
                "           var eventSocket = new WebSocket(url);"
                ""
                "           aardvark.processEvent = function () {"
                "               console.warn(\"websocket not opened yet\");"
                "           }"
                ""
                "           eventSocket.onopen = function () {"
                "               aardvark.processEvent = function () {"
                "                   var sender = arguments[0];"
                "                   var name = arguments[1];"
                "                   var args = [];"
                "                   for (var i = 2; i < arguments.length; i++) {"
                "                       args.push(JSON.stringify(arguments[i]));"
                "                   }"
                "                   var message = JSON.stringify({ sender: sender, name: name, args: args });"
                "                   eventSocket.send(message);"
                "               }"
                "           };"
                ""
                "           eventSocket.onmessage = function (m) {"
                "              eval(\"{\\r\\n\" + m.data + \"\\r\\n}\");"
                "           };"
                ""
                "       </script>"
                "   </head>"
                "   <body style=\"width: 100%; height: 100%; border: 0; padding: 0; margin: 0\">"
                "   </body>"
                "</html>"
            ]

        type WebSocket with
            member x.readMessage() =
                socket {
                    let! (t,d,fin) = x.read()
                    if fin then 
                        return (t,d)
                    else
                        let! (_, rest) = x.readMessage()
                        return (t, Array.append d rest)
                }

        let start (runtime : IRuntime) (port : int) (perform : 'action -> unit) (ui : Ui<'action>) =
            let state =
                {
                    handlers = Dictionary()
                    scenes = Dictionary()
                }

            let events (s : WebSocket) (ctx : HttpContext) =
                let reader = ui.GetReader()
                let mutable initial = true
                let performUpdate (s : WebSocket) =
                    lock reader (fun () ->
                        let expr = reader.Update(AdaptiveToken.Top, Body, state) 
                        let code = expr |> JSExpr.toString
                        let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]

                        if code = "" then
                            ()
                            //Log.line "empty update"
                        else

                            //let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                            //lock runtime (fun () -> 
                            //    Log.start "update"
                            //    for l in lines do Log.line "%s" l
                            //    Log.stop()
                            //)

                            let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes(code))) true |> Async.RunSynchronously
                            match res with
                                | Choice1Of2 () ->
                                    ()
                                | Choice2Of2 err ->
                                    failwithf "[WS] error: %A" err
                    )
            
                let pending = MVar.create()
                let subsription = reader.AddMarkingCallback(fun () -> MVar.put pending ())

            
                let mutable running = true
                let runner =
                    async {
                        while running do
                            let! _ = MVar.takeAsync pending
                            performUpdate s
                    }

                Async.Start runner

                socket {
                    try
                        while running do
                            let! msg = s.readMessage()
                            match msg with
                                | (Opcode.Text, str) ->
                                    let event : Event = Pickler.json.UnPickle str

                                    match state.handlers.TryGetValue ((event.sender, event.name)) with
                                        | (true, f) ->
                                            let action = f (Array.toList event.args)
                                            perform action
                                        | _ ->
                                            ()

                                | (Opcode.Close,_) ->
                                    running <- false
                        
                                | _ ->
                                    ()
                    finally
                        reader.Destroy state
                }

            let parts = 
                [
                    GET >=> path "/main/" >=> OK main
                    GET >=> path "/main/events" >=> handShake events
                ]

            Server.start runtime port parts (fun id ctrl ->
                match state.scenes.TryGetValue id with
                    | (true, f) -> ctrl |> f |> Some
                    | _ -> None
            )
     
     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module App = 

    let startMApp (runtime : IRuntime) (port : int) (app : MApp<'m, 'mm, 'msg>) =
        let imut = Mod.init app.cinitial
        let mut = app.cinit app.cinitial
        let perform (msg : 'msg) =
            let newImut = app.cupdate imut.Value msg
            transact (fun () ->
                imut.Value <- newImut
                app.capply mut newImut
            )

        let view = app.cview mut

        Ui.start runtime port perform view

    let inline start<'model, 'msg, 'mmodel when 'mmodel : (static member Create : 'model -> 'mmodel) and 'mmodel : (member Update : 'model -> unit)> (runtime : IRuntime) (port : int) (app : App<'model, 'mmodel, 'msg>) =   
        let capp = 
            {
                cview = app.view
                cupdate = app.update
                cinit = fun m -> (^mmodel : (static member Create : 'model -> 'mmodel) (m))
                capply = fun mm m -> (^mmodel : (member Update : 'model -> unit) (mm,m))
                cinitial = app.initial
            }

        startMApp runtime port capp

