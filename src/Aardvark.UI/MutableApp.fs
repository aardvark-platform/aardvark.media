namespace Aardvark.UI

open System
open System.IO
open System.Text
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open System.Threading.Tasks

open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open FSharp.Data.Adaptive

type internal Messages<'msg> =
    {
        messages : 'msg seq
        event    : ManualResetEventSlim
    }

type MutableApp<'model, 'mmodel, 'msg>(app: IApp<'model, 'mmodel, 'msg>, unpersist: Unpersist<'model, 'mmodel>) =
    let updateLock = obj()
    let source = new CancellationTokenSource()
    let resources = Stack<IDisposable>()

    let state = AVal.init app.Initial
    let mstate = unpersist.create app.Initial
    let node = app.View mstate :?> DomNode<'msg>

    let messageQueue = List<Messages<'msg>>(128)
    let subject = new FSharp.Control.Event<'msg>()
    let messages = subject.Publish

    let emit (messages: 'msg seq) =
        lock messageQueue (fun () ->
            messageQueue.Add { messages = messages; event = null }
            Monitor.Pulse messageQueue
        )

    let adjustThreads (oldThreads: ThreadPool<'msg>) (newThreads: ThreadPool<'msg>) =
        let merge (_ : string) (oldThread: Command<'msg> voption) (newThread: Command<'msg> voption) =
            match oldThread, newThread with
            | ValueSome o, ValueNone   -> o.Stop(); newThread
            | ValueNone, ValueSome n   -> n.Start(Seq.singleton >> emit); newThread
            | ValueSome _, ValueSome _ -> oldThread
            | ValueNone, ValueNone     -> ValueNone

        ThreadPool<'msg>(HashMap.choose2V merge oldThreads.store newThreads.store)

    let mutable currentThreads =
        let initialThreads = app.Threads app.Initial
        initialThreads |> adjustThreads ThreadPool.empty

    let processMessages (msgs: Messages<'msg> list) =
        if not msgs.IsEmpty then
            let messagesForward = List<_>()

            lock updateLock (fun () ->
                if Config.shouldTimeUpdate then Log.startTimed "[Aardvark.UI] update/adjustThreads/unpersist"

                transact (fun () ->
                    let mutable newState = state.Value
                    for msg in msgs do
                        for msg in msg.messages do
                            newState <-
                                try
                                    app.Update(state.Value, msg)
                                with exn ->
                                    Log.error $"[Aardvark.UI] Update failed: {exn}"
                                    state.Value

                            let newThreads = app.Threads newState
                            currentThreads <- newThreads |> adjustThreads currentThreads

                            state.Value <- newState

                            messagesForward.Add(msg)

                        // if somebody awaits message processing, trigger it
                        if notNull msg.event then
                            msg.event.Set()

                    unpersist.update mstate newState
                )

                if Config.shouldTimeUpdate then Log.stop ()
            )

            for msg in messagesForward do
                subject.Trigger(msg)

    let updateThread =
        let update () =
            while not source.IsCancellationRequested do
                Monitor.Enter(messageQueue)
                while not source.IsCancellationRequested && messageQueue.Count = 0 do
                    Monitor.Wait(messageQueue) |> ignore

                let messages =
                    if not source.IsCancellationRequested then
                        let messages = messageQueue |> CSharpList.toList
                        messages
                    else
                        []

                messageQueue.Clear()

                Monitor.Exit(messageQueue)

                processMessages messages

        let thread = Thread(ThreadStart update)
        thread.Name <- "MutableApp (Update)"
        thread.IsBackground <- true
        thread.Start()
        thread

    member _.Dom = node
    member _.Model = state :> aval<_>
    member _.MutableModel = mstate
    member _.Messages = messages
    member _.UpdateLock = updateLock
    member _.CancellationToken = source.Token

    member this.Update(_: Guid, messages: 'msg seq) =
        //use mri = new System.Threading.ManualResetEventSlim()
        emit messages
        //  mri.Wait()

    member this.UpdateSync(_: Guid, messages: 'msg seq) =
        processMessages [ { messages = messages; event = null } ]

    member _.Register(resource: IDisposable) =
        lock resources (fun _ -> resources.Push resource)

    member _.Dispose() =
        source.Cancel()

        lock messageQueue (fun () -> Monitor.PulseAll messageQueue)
        updateThread.Join()

        lock resources (fun _ ->
            for r in resources do r.Dispose()
            resources.Clear()
        )

        source.Dispose()

    interface IMutableApp<'model, 'msg> with
        member this.Dom = this.Dom
        member this.Model = this.Model
        member this.UpdateLock = this.UpdateLock
        member this.CancellationToken = this.CancellationToken
        member this.Update(session, messages) = this.Update(session, messages)
        member this.Register(resource) = this.Register resource
        member this.Dispose() = this.Dispose()

module MutableApp =
    open Updaters

    let private template =
        let html =
            let ass = typeof<DomNode<_>>.Assembly
            use stream = ass.GetManifestResourceStream("Aardvark.UI.template.html")
            let reader = new StreamReader(stream)
            reader.ReadToEnd()

        fun (title: string) -> html |> String.replace "__TITLE__" title

    [<AutoOpen>]
    module private Internals =

        type ChannelMessage =
            {
                targetId : string
                channel  : string
                data     : string list
            }

        type ChannelMap() =
            let fw = Dictionary<ChannelId, ChannelReader>()
            let bw = Dictionary<ChannelReader, ChannelId>()

            member _.Add(id: ChannelId, reader: ChannelReader) =
                fw.[id] <- reader
                bw.[reader] <- id

            member _.GetAndRemove(id: ChannelId, [<Out>] reader: byref<ChannelReader>) =
                if fw.TryGetValue(id, &reader) then
                    fw.Remove id |> ignore
                    bw.Remove reader |> ignore
                    true
                else
                    false

            member _.TryGetId(reader: ChannelReader, [<Out>] id: byref<ChannelId>) =
                bw.TryGetValue(reader, &id)

        [<Struct>]
        type EventMessage =
            {
                mutable sender  : string
                mutable name    : string
                mutable version : byte voption
                mutable args    : string[]
            }

        module EventMessage =
            open System.Text.Json

            // FSPickler cannot handle options properly, we'd have to write { Some: value } on JS side instead of simply omitting the property
            // For ValueOption it's even more stupid so we use System.Text.Json instead.
            // Also, FSPickler cannot deal with ArraySegments so we'd have to do another copy.
            type private JsonEventMessageConverter() =
                inherit JsonStructConverter<EventMessage>()

                override this.ReadField(reader, name, value, options) =
                    match name with
                    | "sender"  -> value.sender <- reader.GetString()
                    | "name"    -> value.name <- reader.GetString()
                    | "version" -> value.version <- ValueSome <| reader.GetByte()
                    | "args"    -> value.args <- JsonSerializer.Deserialize<string[]>(&reader, options)
                    | _         -> reader.Skip()

            let private serializerOptions =
                let opts = JsonSerializerOptions()
                opts.Converters.Add(JsonEventMessageConverter())
                opts

            let fromJson (data: ArraySegment<byte>) =
                JsonSerializer.Deserialize<EventMessage>(data, serializerOptions)

    let toWebPart' (http: IHttpBackend<'HttpContext, 'HttpHandler>) (runtime: IRuntime) (useGpuCompression: bool) (app: MutableApp<'model, 'mmodel, 'msg>) =
        let sceneStore =
            ConcurrentDictionary<string, SceneInfo<'msg>>()

        let compressor =
            if useGpuCompression then new JpegCompressor(runtime) |> ValueSome
            else ValueNone

        let sessionCount = new CountdownEvent(1)
        let cancellationToken = app.CancellationToken

        let renderServer =
            {
                runtime = runtime

                getScene = fun sceneName ->
                    match sceneStore.TryGetValue sceneName with
                    | true, info -> info.scene
                    | _ -> Scene.empty

                getState = fun clientInfo ->
                    match sceneStore.TryGetValue clientInfo.sceneName with
                    | true, info ->
                        match info.messages with
                        | ValueSome sceneMessages ->
                            let msgs = sceneMessages.preRender clientInfo
                            app.UpdateSync(clientInfo.session, msgs)
                        | _ ->
                            ()

                        info.getState clientInfo
                    | _ ->
                        RenderState.identity

                compressor = compressor

                rendered = fun clientInfo ->
                    match sceneStore.TryGetValue clientInfo.sceneName with
                    | true, info ->
                        match info.messages with
                        | ValueSome sceneMessages ->
                            let msgs = sceneMessages.postRender clientInfo
                            app.UpdateSync(clientInfo.session, msgs)
                        | _ ->
                            ()
                    | _ ->
                        ()
            }

        let events (socket: IWebSocket) (context: 'HttpContext) : Task =
            let request = http.getRequest context

            match request.QueryParam "session" with
            | Some (Guid sessionId) ->
                let updater = app.Dom.NewUpdater(request)

                let handlers = EventHandlers<'msg>()
                let scenes = Dictionary()

                let state : UpdateState<'msg> =
                    {
                        scenes     = ContraDict.ofDictionary scenes
                        handlers   = handlers
                        references = Dictionary()
                        channels   = Dictionary()
                        messages   = app.Messages
                    }

                let activeChannels = ChannelMap()
                let pendingChannels = LockedSet<ChannelReader>()

                let sender =
                    { new AdaptiveObject() with
                        member _.InputChangedObject(_, object) =
                            match object with
                            | :? ChannelReader as reader -> pendingChannels.Add reader |> ignore
                            | _ -> ()
                    }

                let mutable running = true
                let update = MVar.create true
                let subscription = sender.AddMarkingCallback(fun () -> MVar.put update true)

                let handleConnectionError (message: string) (exn: Exception) =
                    match ConnectionError.ofException exn with
                    | ConnectionError.Canceled -> Report.Line(3, $"[Media] Stopping session {sessionId}")
                    | ConnectionError.Closed   -> Report.Line(3, $"[Media] Connection for session {sessionId} closed")
                    | ConnectionError.Lost     -> Report.Line(3, $"[Media] Connection for session {sessionId} lost")
                    | _                        -> Log.warn $"[Media] {message}: {exn.GetBaseException().Message}"

                let send (data: byte[]) =
                    let task =
                        task {
                            try
                                cancellationToken.ThrowIfCancellationRequested()
                                return! socket.Send(WebSocketOpCode.Text, data, cancellationToken)
                            with exn ->
                                handleConnectionError $"Sending update for session {sessionId} failed" exn
                                Volatile.Write(&running, false)
                        }
                    task.Wait()

                let updateFunction () =
                    use _ = sessionCount.Acquire()
                    Report.Line(3, $"[Media] Started UI update thread for session {sessionId}")

                    while MVar.take update && Volatile.Read &running && not cancellationToken.IsCancellationRequested do
                        use _ = app.UpdateLock.Locked

                        sender.EvaluateAlways AdaptiveToken.Top (fun token ->
                            if Config.shouldTimeJsCodeGeneration then
                                Log.startTimed "[Aardvark.UI] generating code (updater.Update + js post processing)"

                            let code =
                                let expr =
                                    if Config.shouldTimeUIUpdate then Log.startTimed "[Aardvark.UI] updating UI"
                                    let js = updater.Update(token, state, ValueSome (fun n -> JSExpr.AppendChild(JSExpr.Body, n)))
                                    if Config.shouldTimeUIUpdate then Log.stop ()
                                    js

                                for name, info in Dictionary.toSeq scenes do
                                    sceneStore.TryAdd(name, info) |> ignore

                                let newReferences = state.references.Values |> Seq.toArray

                                if Config.showTimeJsAssembly then Log.startTimed "[Aardvark.UI] JS assembler"
                                let code = expr |> JSExpr.toString
                                if Config.showTimeJsAssembly then Log.stop()

                                if newReferences.Length > 0 then
                                    let args =
                                        newReferences |> Seq.map (fun r ->
                                            let kind =
                                                match r.kind with
                                                | Script     -> "script"
                                                | Stylesheet -> "stylesheet"
                                                | Module     -> "module"

                                            $"{{ kind: \"{kind}\", name: \"{r.name}\", url: \"{r.url}\" }}"
                                        ) |> String.concat ","

                                    $"aardvark.addReferences([{args}], function() {{ {code} }});"
                                else
                                    code

                            if Config.shouldTimeJsCodeGeneration then
                                Log.line "[Aardvark.UI] code length: %d" code.Length
                                Log.stop()

                            if code <> "" then
                                if Config.shouldPrintDOMUpdates then
                                    Log.start "update"
                                    Log.line "%s" code
                                    Log.stop()

                                match Config.dumpJsCodeFile with
                                | None -> ()
                                | Some file ->
                                    try
                                        File.AppendAllText(file, code)
                                    with e ->
                                        printfn "%A" e

                                let tag = if state.references.Count > 0 then "r" else "x"
                                send <| Encoding.UTF8.GetBytes(tag + code)

                            // Handle added and removed channels
                            for KeyValue(id, reader) in state.channels do
                                if isNull reader then
                                    match activeChannels.GetAndRemove id with
                                    | true, reader ->
                                        pendingChannels.Remove reader |> ignore
                                        reader.Outputs.Remove sender |> ignore
                                        reader.Dispose()
                                    | _ -> ()

                                    let message = Pickler.json.Pickle { targetId = id.ElementId; channel = id.ChannelName; data = ["\"commit-suicide\""] }
                                    send message
                                else
                                    pendingChannels.Add reader |> ignore
                                    activeChannels.Add(id, reader)

                            // Send messages for out-of-date channels
                            for reader in pendingChannels.GetAndClear() do
                                match activeChannels.TryGetId reader with
                                | true, id ->
                                    let messages =
                                        try
                                            reader.GetMessages token
                                        with exn ->
                                            Log.error $"[Media] Failed to get '{id.ChannelName}' messages for {id.ElementId}: {exn}"
                                            []

                                    match messages with
                                    | [] -> ()
                                    | messages ->
                                        let message = Pickler.json.Pickle { targetId = id.ElementId; channel = id.ChannelName; data = messages }
                                        send message

                                | _ -> ()
                        )

                        state.references.Clear()
                        state.channels.Clear()

                    Report.Line(3, $"[Media] Stopped UI update thread for session {sessionId}")

                let updateThread = Thread(ThreadStart updateFunction)
                updateThread.IsBackground <- true
                updateThread.Name <- $"MutableApp (UI Update - {sessionId})"
                updateThread.Start()

                task {
                    Report.Line(3, $"[Media] Created session {sessionId}")
                    use _ = sessionCount.Acquire()

                    let buffer = SocketBuffer(128)

                    while Volatile.Read &running && not cancellationToken.IsCancellationRequested do
                        try
                            buffer.Position <- 0

                            let! message = socket.Receive(buffer, cancellationToken)
                            let data = buffer.Data

                            match message with
                            | WebSocketOpCode.Text when data.Count > 0 ->
                                if data.[0] = uint8 '#' then
                                    do! socket.SendPong cancellationToken
                                else
                                    try
                                        let evt = EventMessage.fromJson data
                                        let key = ChannelId(evt.sender, evt.name)

                                        match handlers.TryGet(key, evt.version) with
                                        | ValueSome handler when notNull evt.args ->
                                            let args = Array.toList evt.args

                                            let msgs =
                                                try
                                                    handler.invoke sessionId evt.sender args
                                                with exn ->
                                                    Log.error $"[Media] Event handler '{evt.name}' for {evt.sender} faulted (args: {args}): {exn}"
                                                    Seq.empty

                                            app.Update(sessionId, msgs)

                                        | _ -> ()

                                    with exn ->
                                        let str = Encoding.UTF8.TryGetString data
                                        if notNull str then
                                            Log.error $"[Media] Failed to process event message '{str}': {exn}"
                                        else
                                            Log.error $"[Media] Failed to process event message: {exn}"

                            | WebSocketOpCode.Close ->
                                Report.Line(3, $"[Media] Event socket for session {sessionId} closed")
                                do! socket.Close cancellationToken
                                Volatile.Write(&running, false)

                            | WebSocketOpCode.Ping ->
                                do! socket.SendPong cancellationToken

                            | _ ->
                                Log.warn $"[Media] Unexpected event message: {message}"

                        with exn ->
                            handleConnectionError "Event socket I/O failed" exn
                            Volatile.Write(&running, false)

                    MVar.put update false
                    updateThread.Join()

                    updater.Destroy(state, JSExpr.Body) |> ignore
                    subscription.Dispose()
                    socket.Dispose()

                    Report.Line(3, $"[Media] Closed session {sessionId}")
                }

            | _ ->
                Task.FromException(Exception("Request does not contain a session id."))

        app.Register {
            new IDisposable with
                member x.Dispose() =
                    sessionCount.Signal() |> ignore
                    sessionCount.Wait()
                    sessionCount.Dispose()
        }

        let (>=>) a b = http.compose a b

        http.choose [
            http.subRoute "/rendering" (RenderServer.toWebPart http app renderServer)
            http.route "/events" >=> http.handShake events
            http.route "/" >=> http.html (template Config.defaultDocumentTitle)
            http.assembly typeof<MutableApp<_, _, _>>.Assembly
        ]

    let toWebPart (http: IHttpBackend<'HttpContext, 'HttpHandler>) (runtime: IRuntime) (app: MutableApp<'model, 'mmodel, 'msg>) =
        toWebPart' http runtime false app