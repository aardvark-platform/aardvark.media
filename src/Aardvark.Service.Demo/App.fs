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
        let private main = File.readAllText @"template.html"
            //String.concat "\r\n" [
            //    "<html>"
            //    "   <head>"
            //    "       <title>Aardvark rocks \\o/</title>"
            //    "       <script src=\"https://code.jquery.com/jquery-3.1.1.min.js\"></script>"
            //    "       <script src=\"https://cdnjs.cloudflare.com/ajax/libs/jquery-resize/1.1/jquery.ba-resize.min.js\"></script>"
            //    "       <script src=\"/aardvark.js\"></script>"
            //    "       <script>"
            //    "           var aardvark = {};"
            //    "           var _refs = {};"
            //    "           aardvark.referencedScripts = _refs;"
            //    "           aardvark.referencedScripts[\"jquery\"] = true;"
            //    "           aardvark.addReference = function(name, url) {"
            //    "               if(!aardvark.referencedScripts[name]) {"
            //    "                   aardvark.referencedScripts[name] = true;"
            //    "                   var script = document.createElement(\"script\");"
            //    "                   script.setAttribute(\"src\", url);"
            //    "                   var loaded = false;"
            //    "                   script.onloaded = function() { loaded = true; };"
            //    "                   document.head.appendChild(script);"
            //    "                   while(!loaded) {}"
            //    "               }"
            //    "           };"
            //    "           "
            //    "           function getUrl(proto, subpath) {"
            //    "               var l = window.location;"
            //    "               var path = l.pathname;"
            //    "               if(l.port === \"\") {"
            //    "                   return proto + l.hostname + path + subpath;"
            //    "               }"
            //    "               else {"
            //    "                   return proto + l.hostname + ':' + l.port + path + subpath;"
            //    "               }"
            //    "           }"
            //    "           var url = getUrl('ws://', 'events');"
            //    "           var eventSocket = new WebSocket(url);"
            //    ""
            //    "           aardvark.processEvent = function () {"
            //    "               console.warn(\"websocket not opened yet\");"
            //    "           }"
            //    ""
            //    "           eventSocket.onopen = function () {"
            //    "               aardvark.processEvent = function () {"
            //    "                   var sender = arguments[0];"
            //    "                   var name = arguments[1];"
            //    "                   var args = [];"
            //    "                   for (var i = 2; i < arguments.length; i++) {"
            //    "                       args.push(JSON.stringify(arguments[i]));"
            //    "                   }"
            //    "                   var message = JSON.stringify({ sender: sender, name: name, args: args });"
            //    "                   eventSocket.send(message);"
            //    "               }"
            //    "           };"
            //    ""
            //    "           eventSocket.onmessage = function (m) {"
            //    "              eval(\"{\\r\\n\" + m.data + \"\\r\\n}\");"
            //    "           };"
            //    ""
            //    "       </script>"
            //    "   </head>"
            //    "   <body style=\"width: 100%; height: 100%; border: 0; padding: 0; margin: 0\">"
            //    "   </body>"
            //    "</html>"
            //]

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
                    references = Dictionary()
                    activeChannels = Dictionary()
                }

            let events (s : WebSocket) (ctx : HttpContext) =
                let mutable existingChannels = Dictionary<string * string, Channel>()

                let reader = ui.GetReader()
                let self =
                    Mod.custom (fun self ->
                        let performUpdate (s : WebSocket) =
                            lock reader (fun () ->
                                let expr = reader.Update(self, Body, state) 
                                let code = expr |> JSExpr.toString
                                let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]

                                let newReferences = state.references.Values |> Seq.toArray
                                state.references.Clear()

                                let code = 
                                    if newReferences.Length > 0 then
                                        let args = 
                                            newReferences |> Seq.map (fun r -> 
                                                sprintf "{ kind: \"%s\", name: \"%s\", url: \"%s\" }" (if r.kind = Script then "script" else "stylesheet") r.name r.url
                                            ) |> String.concat "," |> sprintf "[%s]" 
                                        let code = String.indent 1 code
                                        sprintf "aardvark.addReferences(%s, function() {\r\n%s\r\n});" args code
                                    else
                                        code

                                if code <> "" then
                                    //let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                                    //lock runtime (fun () -> 
                                    //    Log.start "update"
                                    //    for l in lines do Log.line "%s" l
                                    //    Log.stop()
                                    //)

                                    let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("x" + code))) true |> Async.RunSynchronously
                                    match res with
                                        | Choice1Of2 () ->
                                            ()
                                        | Choice2Of2 err ->
                                            failwithf "[WS] error: %A" err

                                let newChannels = Dictionary()
                                for KeyValue((id,name),channel) in state.activeChannels do
                                    newChannels.[(id,name)] <- channel
                                    existingChannels.Remove(id, name) |> ignore
                                    let message = channel.GetMessage(self, id)
                                    match message with
                                        | Some message ->
                                            let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("c" + Pickler.json.PickleToString message))) true |> Async.RunSynchronously
                                            match res with
                                                | Choice1Of2 () ->
                                                    ()
                                                | Choice2Of2 err ->
                                                    failwithf "[WS] error: %A" err
                                        | None -> 
                                            ()

                                for KeyValue((id,name),channel) in existingChannels do
                                    let suicide = { targetId = id; channel = name; data = "commit-suicide" }
                                    s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("c" + Pickler.json.PickleToString suicide))) true |> Async.RunSynchronously |> ignore

                                    channel.Dispose()

                                existingChannels <- newChannels
                        )

                        performUpdate s
                    )

                let pending = MVar.create()
                let subsription = self.AddMarkingCallback(MVar.put pending)

            
                let mutable running = true
                let runner =
                    async {
                        while running do
                            let! _ = MVar.takeAsync pending
                            self.GetValue(AdaptiveToken.Top)
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
                        reader.Destroy(state, JSExpr.GetElementById reader.Id) |> ignore
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

