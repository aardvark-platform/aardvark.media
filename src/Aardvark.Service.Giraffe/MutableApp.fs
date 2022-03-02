namespace Aardvark.UI.Giraffe

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open System.Net.WebSockets

open Giraffe
open FSharp.Data.Adaptive
open FSharp.Control.Tasks

open Aardvark.UI
open Aardvark.Service
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Base

open Aardvark.Service.Giraffe
open Aardvark.UI.Internal


module MutableApp =
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



    type private DummyObject() =
        inherit AdaptiveObject()

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
            match context.Request.Query.TryGetValue "session" with
                | (true, SingleString (Guid sessionId)) ->

                    let request = 
                        {
                            requestPath = context.Request.Path.Value
                            queryParams = context.Request.Query |> Seq.choose (fun v -> match v.Value with | SingleString s -> Some (v.Key, s) | _ -> None) |> Map.ofSeq
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

                    let o = DummyObject()
                    
                    let update = MVar.create true
                    let subscription = o.AddMarkingCallback(fun () -> MVar.put update true)
                    
                    let mutable running = true
                    let mutable oldChannels : Set<string * string> = Set.empty

                    let send (arr : byte[]) =
                        let rec res retries = 
                            task {
                                try 
                                    return! ws.SendAsync(ArraySegment(arr), WebSocketMessageType.Text, true, CancellationToken.None) 
                                with e -> 
                                    Log.warn "[Media] send failed: %s (retries=%d)" e.Message retries
                                    if retries >= 0 then 
                                        do! Async.Sleep 100
                                        return! res (retries - 1)
                                    else   
                                        raise e
                            }
                        task {
                            try 
                                return! res 10 
                            with err ->
                                failwithf "[WS] error: %A" err
                        }
                                                
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
                            
                                                if Config.showTimeJsAssembly then Log.startTimed "[Aardvark.UI] JS assembler"
                                                let code = expr |> JSExpr.toString
                                                let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]
                                                if Config.showTimeJsAssembly then Log.stop()
                                            
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
                                                    match Config.dumpJsCodeFile with
                                                        | None -> ()
                                                        | Some file -> 
                                                            try
                                                                System.IO.File.AppendAllText(file,code)
                                                            with e -> 
                                                                printfn "%A" e
                                                )

                                                
                                                let r = send (Text.Encoding.UTF8.GetBytes("x" + code))
                                                r.Result |> ignore

    
                                                
                                            let mutable o = oldChannels
                                            let mutable c = Set.empty
                                            for (KeyValue((id,name), cr)) in state.activeChannels do
                                                match cr.GetMessages(t) with
                                                    | [] -> ()
                                                    | messages ->
                                                        let message = Pickler.json.Pickle { targetId = id; channel = name; data = messages }
                                                        send message |> Task.getResult |> ignore

                                                c <- Set.add (id, name) c
                                                o <- Set.remove (id, name) o

                                            for (id, name) in o do
                                                let message = Pickler.json.Pickle { targetId = id; channel = name; data = [Pickler.json.PickleToString "commit-suicide"] }
                                                send message |> Task.getResult |> ignore
                                            
                                            oldChannels <- c
                                        )
                                    )
                          with e -> 
                            Config.updateThreadFailed e
                            ()
                            //raise e


                    let updateThread = Thread(ThreadStart updateFunction)
                    updateThread.IsBackground <- true
                    updateThread.Name <- "[media] UpdateThread"
                    updateThread.Start()
                    
                    let read buffer =
                        async {
                            try
                                let! r = ws.ReceiveAsync(ArraySegment(buffer), CancellationToken.None)
                                return Choice1Of2 r
                            with e ->
                                return Choice2Of2 e
                        }

                    task {
                        while running do
                            let buffer = Array.zeroCreate 1024
                            let! result = read buffer
                            match result with
                            | Choice1Of2 result -> 
                                let data = Array.sub buffer 0 result.Count
                                if result.CloseStatus.HasValue then
                                    running <- true
                                else
                                    match result.MessageType with
                                        | WebSocketMessageType.Text ->
                                            try
                                                if data.Length > 0 && data.[0] = uint8 '#' then
                                                    let str = System.Text.Encoding.UTF8.GetString(data, 1, data.Length - 1)
                                                    match str with
                                                        | "ping" -> 
                                                            () // ignore in giraffe backend
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

                                        | _ ->
                                            Log.warn "[MutableApp] unknown message: %A" (code,data)
                            | Choice2Of2 e -> 
                                if ws.State <> WebSocketState.Open then 
                                    Log.warn "[MutableApp] websocket seams dead (%A), updated going down." e
                                    running <- false
                        
                        MVar.put update false
                        updater.Destroy(state, JSExpr.Body) |> ignore
                        subscription.Dispose()
                    }
                | _ ->
                    failwith "no session id"

        choose [    
            subRoute "/rendering" (Aardvark.Service.Giraffe.Server.toWebPart app.lock renderer)
            Reflection.assemblyWebPart typeof<EmbeddedResources>.Assembly
            route "/events" >=> Websockets.handShake events


            route "/" >=> htmlString template
            htmlString template
        ]

    let toWebPart (runtime : IRuntime) (app : MutableApp<'model, 'msg>) =
        toWebPart' runtime false app


