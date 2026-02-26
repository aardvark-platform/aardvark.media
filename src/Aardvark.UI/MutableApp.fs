namespace Aardvark.UI

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Service
open Aardvark.UI.Internal

open Suave
open Suave.Filters
open Suave.Operators
open Suave.WebSocket
open Suave.Successful
open Suave.Sockets.Control
open Suave.Sockets

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

    let mutable shouldAdjust = true
    
    let adjust () =
        if shouldAdjust then
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
    open Aardvark.UI.Internal.Updaters
    
    let private template =
        let html =
            let ass = typeof<DomNode<_>>.Assembly
            use stream = ass.GetManifestResourceStream("Aardvark.UI.template.html")
            let reader = new IO.StreamReader(stream)
            reader.ReadToEnd()

        fun (title: string) -> html |> String.replace "__TITLE__" title

    [<AutoOpen>]
    module Internals =

        type EventMessage =
            {
                sender  : string
                name    : string
                version : byte voption
                args    : string[]
            }

        module EventMessage =
            open System.Text.Json
            open System.Text.Json.Serialization

            // FSPickler cannot handle options properly, we'd have to write { Some: value } on JS side instead of simply omitting the property
            // For ValueOption it's even more stupid so we use System.Text.Json instead...
            type private EventMessageConverter() =
                inherit JsonConverter<EventMessage>()

                override this.Write(_, _, _) =
                    raise <| NotImplementedException()

                override this.Read(reader, _, options) =
                    let mutable sender = ""
                    let mutable name = ""
                    let mutable version = ValueNone
                    let mutable args = [||]

                    while reader.Read() && reader.TokenType <> JsonTokenType.EndObject do
                        match reader.TokenType with
                        | JsonTokenType.PropertyName ->
                            let propertyName = reader.GetString()
                            reader.Read() |> ignore

                            match propertyName with
                            | "sender"  -> sender <- reader.GetString()
                            | "name"    -> name <- reader.GetString()
                            | "version" -> version <- ValueSome <| reader.GetByte()
                            | "args"    -> args <- JsonSerializer.Deserialize<string[]>(&reader, options)
                            | _         -> reader.Skip()

                        | token ->
                            raise <| JsonException($"Expected property name but got {token}.")

                    { sender = sender; name = name; version = version; args = args }

            let private serializerOptions =
                let opts = JsonSerializerOptions()
                opts.Converters.Add(EventMessageConverter())
                opts

            let fromJson (data: byte[]) =
                JsonSerializer.Deserialize<EventMessage>(data, serializerOptions)

    let private (|Guid|_|) (str : string) =
        match Guid.TryParse str with
            | (true, guid) -> Some guid
            | _ -> None



    type private DummyObject() =
        inherit AdaptiveObject()

    let toWebPart' (runtime : IRuntime) (useGpuCompression : bool) (app : MutableApp<'model, 'msg>) =
        
        ThreadPoolAdjustment.adjust ()

        let sceneStore =
            ConcurrentDictionary<string, Scene * Option<SceneMessages<'msg>> * (ClientInfo -> ClientState)>()


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
                                | Some sceneMessages ->
                                    let msgs = sceneMessages.preRender clientInfo
                                    if not (Seq.isEmpty msgs) then
                                        app.updateSync clientInfo.session msgs
                                | None ->
                                    ()

                            Some (cam clientInfo)
                        | _ -> 
                            None

                compressor = compressor

                rendered = fun clientInfo ->
                    match sceneStore.TryGetValue clientInfo.sceneName with
                        | (true, (scene, update, cam)) -> 
                            match update with
                                | Some sceneMessages -> 
                                    let msgs = sceneMessages.postRender clientInfo
                                    if not (Seq.isEmpty msgs) then
                                        app.updateSync clientInfo.session msgs
                                | None ->
                                    ()
                        | _ ->
                            ()
                    

                fileSystemRoot = None //Some "/"
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
                    
                    let handlers = ConcurrentDictionary()
                    let scenes = Dictionary()

                    let state : UpdateState<'msg> =
                        {
                            scenes          = ContraDict.ofDictionary scenes
                            handlers        = ContraDict.ofConcurrentDictionary handlers
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
                            async {
                                try 
                                    return! ws.send Opcode.Text (ByteSegment(arr)) true 
                                with e -> 
                                    Log.warn "[Media] send failed: %s (retries=%d)" e.Message retries
                                    do! Async.Sleep 100
                                    return! res (retries - 1)
                            }
                        let res = res 10 |> Async.RunSynchronously
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
                                                    state.references.Clear()
                                                    if Config.shouldTimeUIUpdate then Log.startTimed "[Aardvark.UI] updating UI"
                                                    let r = updater.Update(t,state, Some (fun n -> JSExpr.AppendChild(JSExpr.Body, n)))
                                                    if Config.shouldTimeUIUpdate then Log.stop ()
                                                    r

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
                                                            let kind =
                                                                match r.kind with
                                                                | Script -> "script"
                                                                | Stylesheet -> "stylesheet"
                                                                | Module -> "module"
                                                            sprintf "{ kind: \"%s\", name: \"%s\", url: \"%s\" }" kind r.name r.url
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

                                                let tag = if state.references.Count > 0 then "r" else "x"
                                                send (Text.Encoding.UTF8.GetBytes(tag + code))
    
                                                
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
                            Config.updateThreadFailed e
                            ()
                            //raise e


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
                                            let evt = EventMessage.fromJson data
                                            let key = evt.sender, evt.name

                                            match handlers.TryGetValue key with
                                            | true, handler ->
                                                let version =
                                                    evt.version |> ValueOption.defaultValue handler.version

                                                if version = handler.version then
                                                    let msgs = handler.invoke sessionId evt.sender (Array.toList evt.args)
                                                    app.update sessionId msgs

                                            | _ -> ()

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
            path "/" >=> OK (template Config.defaultDocumentTitle)
        ]

    let toWebPart (runtime : IRuntime) (app : MutableApp<'model, 'msg>) =
        toWebPart' runtime false app





