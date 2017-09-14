namespace Aardvark.UI

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Service
open Aardvark.UI.Internal

open Suave
open Suave.Control
open Suave.Filters
open Suave.Operators
open Suave.WebSocket
open Suave.Successful
open Suave.Sockets.Control
open Suave.Sockets

[<AutoOpen>]
module private Tools =
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

type MutableApp<'model, 'msg> =
    {
        lock        : obj
        model       : IMod<'model>
        ui          : DomNode<'msg>
        update      : Guid -> seq<'msg> -> unit
    }

module MutableApp =
    
    let private template = 
        let ass = typeof<DomNode<_>>.Assembly
        use stream = ass.GetManifestResourceStream("template.html")
        let reader = new IO.StreamReader(stream)
        reader.ReadToEnd()

    type private EventMessage =
        {
            sender  : string
            name    : string
            args    : array<string>
        }

    let private (|Guid|_|) (str : string) =
        match Guid.TryParse str with
            | (true, guid) -> Some guid
            | _ -> None



    let toWebPart (runtime : IRuntime) (app : MutableApp<'model, 'msg>) =
        
        let sceneStore =
            ConcurrentDictionary<string, Scene * (ClientInfo -> ClientState)>()

        
        let renderer =
            {
                runtime = runtime
                content = fun sceneName ->
                    match sceneStore.TryGetValue sceneName with
                        | (true, (scene, cam)) -> Some scene
                        | _ -> None

                getState = fun clientInfo ->
                    match sceneStore.TryGetValue clientInfo.sceneName with
                        | (true, (scene, cam)) -> Some (cam clientInfo)
                        | _ -> None
            }


        let events (ws : WebSocket) (context: HttpContext) =
            match context.request.queryParam "session" with
                | Choice1Of2 (Guid sessionId) ->
                    let updater = app.ui.NewUpdater()
                    let state =
                        {
                            scenes      = Dictionary()
                            handlers    = Dictionary()
                            references  = Dictionary()
                        }

                    
                    let update = MVar.create true
                    let subscription = updater.AddMarkingCallback(fun () -> MVar.put update true)
                    
                    let mutable running = true

                    let updateThread =
                        async {
                            while running do
                                let! cont = MVar.takeAsync update
                                if cont then

                                    if Config.shouldTimeJsCodeGeneration then Log.startTimed "[Aardvark.UI] generating code (updater.Update + js post processing)"
                                    let code = 
                                        lock app.lock (fun () ->
                                            let expr = 
                                                lock state (fun () -> 
                                                    state.references.Clear()
                                                    if Config.shouldTimeUIUpdate then Log.startTimed "[Aardvark.UI] updating UI"
                                                    let r = updater.Update(AdaptiveToken.Top, JSExpr.Body, state)
                                                    if Config.shouldTimeUIUpdate then Log.stop ()
                                                    r
                                                )

                                            for (name, sd) in Dictionary.toSeq state.scenes do
                                                sceneStore.TryAdd(name, sd) |> ignore

                                            let newReferences = state.references.Values |> Seq.toArray
                            

                                            let code = expr |> JSExpr.toString
                                            let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]
                                            
                                            if newReferences.Length > 0 then
                                                let args = 
                                                    newReferences |> Seq.map (fun r -> 
                                                        sprintf "{ kind: \"%s\", name: \"%s\", url: \"%s\" }" (if r.kind = Script then "script" else "stylesheet") r.name r.url
                                                    ) |> String.concat "," |> sprintf "[%s]" 
                                                let code = String.indent 1 code
                                                sprintf "aardvark.addReferences(%s, function() {\r\n%s\r\n});" args code
                                            else
                                                code
                                        )

                                    if code <> "" then
                                        let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                                        lock app (fun () -> 
                                            if Config.shouldPrintDOMUpdates then
                                                Log.start "update"
                                                for l in lines do Log.line "%s" l
                                                Log.stop()
                                        )
                                    if Config.shouldTimeJsCodeGeneration then Log.line "[Aardvark.UI] code lenght: %d" (code.Length); Log.stop()

                                    let res = ws.send Opcode.Text (ByteSegment(Text.Encoding.UTF8.GetBytes("x" + code))) true |> Async.RunSynchronously
                                    match res with
                                        | Choice1Of2 () ->
                                            ()
                                        | Choice2Of2 err ->
                                            failwithf "[WS] error: %A" err
                        }

                    Async.Start updateThread

                    socket {
                        while running do
                            let! code, data = ws.readMessage()
                            match code with
                                | Opcode.Text ->
                                    try
                                        let evt : EventMessage = Pickler.json.UnPickle data
                                        match lock state (fun () -> state.handlers.TryGetValue((evt.sender, evt.name))) with
                                            | (true, handler) ->
                                                let messages = handler sessionId evt.sender (Array.toList evt.args)
                                                app.update sessionId messages
                                            | _ ->
                                                ()

                                    with e ->
                                        Log.warn "unpickle faulted: %A" e

                                | Opcode.Close ->
                                    running <- false

                                | _ ->
                                    Log.warn "asdasdsad"
                        
                        MVar.put update false
                        updater.Destroy(state, JSExpr.Body) |> ignore
                        subscription.Dispose()
                    }
                | _ ->
                    SocketOp.abort(Error.InputDataError(None, "no session id")) 

        choose [
            path "/events" >=> handShake events
            path "/" >=> OK template
            prefix "/rendering" >=> Aardvark.Service.Server.toWebPart app.lock renderer
        ]







