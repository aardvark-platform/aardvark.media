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
open Suave.State.CookieStateStore

type EmbeddedResources = EmbeddedResources

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

// https://github.com/aardvark-platform/aardvark.media/issues/19
module ThreadPoolAdjustment =
    
    let adjust () =
        let mutable maxThreads,maxIOThreads = 0,0
        System.Threading.ThreadPool.GetMaxThreads(&maxThreads,&maxIOThreads)
        let mutable minThreads, minIOThreads = 0,0
        System.Threading.ThreadPool.GetMinThreads(&minThreads,&minIOThreads)
        if minThreads < 12 || minIOThreads < 12 then
            Log.warn "[aardvark.media] currently ThreadPool.MinThreads is (%d,%d)" minThreads minIOThreads
            let minThreads   = max 12 minThreads
            let minIOThreads = max 12 minIOThreads
            Log.warn "[aardvark.media] unfortunately, currently we need to adjust this to at least (12,12) due to an open issue https://github.com/aardvark-platform/aardvark.media/issues/19"
            if not <| System.Threading.ThreadPool.SetMinThreads(minThreads, minIOThreads) then Log.warn "could not set min threads"
            if maxThreads < 12 || maxIOThreads < 12 then
                Log.warn "[aardvark.media] detected less than 12 threadpool threads: (%d,%d). Be aware that this will result in severe stutters... Consider switching back to the default (65537,1000)." maxThreads maxIOThreads


module MutableApp =
    open System.Reactive.Subjects
    open Aardvark.UI.Internal.Updaters
    
    let private template = 
        let ass = typeof<DomNode<_>>.Assembly
        use stream = ass.GetManifestResourceStream("Aardvark.UI.template.html")
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



    let toWebPart' (runtime : IRuntime) (useGpuCompression : bool) (app : MutableApp<'model, 'msg>) =
        
        ThreadPoolAdjustment.adjust ()

        let sceneStore =
            ConcurrentDictionary<string, Scene * Option<ClientInfo -> seq<'msg>> * (ClientInfo -> ClientState)>()

        let compressor =
            if useGpuCompression then new JpegCompressor(runtime) |> Some
            else None
         
        let renderer =
            {
                runtime = runtime
                content = fun sceneName ->
                    match sceneStore.TryGetValue sceneName with
                        | (true, (scene, _, _)) -> Some scene
                        | _ -> None

                getState = fun clientInfo ->
                    match sceneStore.TryGetValue clientInfo.sceneName with
                        | (true, (scene, update, cam)) -> 
                            match update with
                                | Some update -> 
                                    let msgs = update clientInfo
                                    if not (Seq.isEmpty msgs) then
                                        app.updateSync clientInfo.session msgs
                                | None ->
                                    ()

                            Some (cam clientInfo)
                        | _ -> 
                            None

                compressor = compressor

                fileSystemRoot = Some "/"
            }

        let events (ws : WebSocket) (context: HttpContext) =
            match context.request.queryParam "session" with
                | Choice1Of2 (Guid sessionId) ->

                    let request = 
                        {
                            requestPath = context.request.path
                            queryParams = context.request.query |> List.choose (function (k, Some v) -> Some (k,v) | _ -> None) |> Map.ofList
                        }

                    let updater = app.ui.NewUpdater(request)
                    
                    let handlers = Dictionary()
                    let scenes = Dictionary()

                    let state : UpdateState<'msg> =
                        {
                            scenes          = ContraDict.ofDictionary scenes
                            handlers        = ContraDict.ofDictionary handlers
                            references      = Dictionary()
                            activeChannels  = Dict()
                            messages        = app.messages
                        }

                    let o = AdaptiveObject()
                    
                    let update = MVar.create true
                    let subscription = o.AddMarkingCallback(fun () -> MVar.put update true)
                    
                    let mutable running = true
                    let mutable oldChannels : Set<string * string> = Set.empty

                    let send (arr : byte[]) =
                        let res = ws.send Opcode.Text (ByteSegment(arr)) true |> Async.RunSynchronously
                        match res with
                            | Choice1Of2 () ->
                                ()
                            | Choice2Of2 err ->
                                failwithf "[WS] error: %A" err
                                                
                    let updateFunction () =
                        try
                            while running do
                                let cont = MVar.take update
                                if cont then
                                    lock app.lock (fun () ->
                                        o.EvaluateAlways AdaptiveToken.Top (fun t ->
                                            if Config.shouldTimeJsCodeGeneration then 
                                                Log.startTimed "[Aardvark.UI] generating code (updater.Update + js post processing)"
   
                                            let code = 
                                                let expr = 
                                                    lock state (fun () -> 
                                                        state.references.Clear()
                                                        if Config.shouldTimeUIUpdate then Log.startTimed "[Aardvark.UI] updating UI"
                                                        let r = updater.Update(t,state, Some (fun n -> JSExpr.AppendChild(JSExpr.Body, n)))
                                                        if Config.shouldTimeUIUpdate then Log.stop ()
                                                        r
                                                    )

                                                for (name, sd) in Dictionary.toSeq scenes do
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

                                            if Config.shouldTimeJsCodeGeneration then 
                                                Log.line "[Aardvark.UI] code length: %d" (code.Length); Log.stop()

                                            if code <> "" then
                                                lock app (fun () -> 
                                                    if Config.shouldPrintDOMUpdates then
                                                        let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                                                        Log.start "update"
                                                        for l in lines do Log.line "%s" l
                                                        Log.stop()
                                                )

                                                send (Text.Encoding.UTF8.GetBytes("x" + code))
    
                                                
                                            let mutable o = oldChannels
                                            let mutable c = Set.empty
                                            for (KeyValue((id,name), cr)) in state.activeChannels do
                                                match cr.GetMessages(t) with
                                                    | [] -> ()
                                                    | messages ->
                                                        let message = Pickler.json.Pickle { targetId = id; channel = name; data = messages }
                                                        send message

                                                c <- Set.add (id, name) c
                                                o <- Set.remove (id, name) o

                                            for (id, name) in o do
                                                let message = Pickler.json.Pickle { targetId = id; channel = name; data = [Pickler.json.PickleToString "commit-suicide"] }
                                                send message
                                            
                                            oldChannels <- c
                                        )
                                    )
                          with e -> 
                            Log.error "[Media] UI update thread died (exn in view function?) : \n%A" e
                            raise e


                    let updateThread = Thread(ThreadStart updateFunction)
                    updateThread.IsBackground <- true
                    updateThread.Name <- "[media] UpdateThread"
                    updateThread.Start()
                    

                    socket {
                        while running do
                            let! code, data = ws.readMessage()
                            match code with
                                | Opcode.Text ->
                                    try
                                        if data.Length > 0 && data.[0] = uint8 '#' then
                                            let str = System.Text.Encoding.UTF8.GetString(data, 1, data.Length - 1)
                                            match str with
                                                | "ping" -> 
                                                    do! ws.send Opcode.Pong (ByteSegment([||])) true
                                                | _ ->
                                                    Log.warn "bad opcode: %A" str
                                        else
                                            let evt : EventMessage = Pickler.json.UnPickle data
                                            match lock state (fun () -> handlers.TryGetValue((evt.sender, evt.name))) with
                                                | (true, handler) ->
                                                    let msgs = handler sessionId evt.sender (Array.toList evt.args)
                                                    app.update sessionId msgs
                                                    
                                                | _ ->
                                                    ()

                                    with e ->
                                        Log.warn "unpickle faulted: %A" e

                                | Opcode.Close ->
                                    running <- false

                                | Opcode.Ping -> 
                                    do! ws.send Opcode.Pong (ByteSegment([||])) true

                                | _ ->
                                    Log.warn "[MutableApp] unknown message: %A" (code,data)
                        
                        MVar.put update false
                        updater.Destroy(state, JSExpr.Body) |> ignore
                        subscription.Dispose()
                    }
                | _ ->
                    SocketOp.abort(Error.InputDataError(None, "no session id")) 

        choose [            
            prefix "/rendering" >=> Aardvark.Service.Server.toWebPart app.lock renderer
            Reflection.assemblyWebPart typeof<EmbeddedResources>.Assembly
            path "/events" >=> handShake events
            path "/" >=> OK template 
        ]

    let toWebPart (runtime : IRuntime) (app : MutableApp<'model, 'msg>) =
        toWebPart' runtime false app





