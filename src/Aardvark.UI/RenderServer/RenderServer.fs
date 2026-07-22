namespace Aardvark.UI

open System
open System.Collections.Generic
open System.Text
open System.Threading
open System.Threading.Tasks

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open System.Diagnostics

type internal RenderServer =
    {
        runtime    : IRuntime
        getScene   : string -> Scene
        getState   : RenderClientInfo -> RenderState
        rendered   : RenderClientInfo -> unit
        compressor : JpegCompressor voption
        onError    : InternalErrorSource -> exn -> unit
    }

type internal RenderClientCreateInfo =
    {
        id               : RenderClientId
        server           : RenderServer
        socket           : IWebSocket
        signature        : IFramebufferSignature
        sceneName        : string
        getScene         : IFramebufferSignature -> string -> ConcreteScene
        useMapping       : bool
        targetQuality    : RenderQuality
    }

    member inline this.runtime    = this.server.runtime
    member inline this.compressor = this.server.compressor

type internal RenderClient(app: IMutableApp, createInfo: RenderClientCreateInfo) =
    static let mutable currentId = 0

    static let newRenderTask (id: int) (info: RenderClientCreateInfo) =
        if info.useMapping then
            new MappedClientRenderTask(info.runtime, id, info.getScene) :> ClientRenderTask
        else
            new JpegClientRenderTask(info.runtime, info.compressor, id, info.getScene, info.targetQuality) :> ClientRenderTask

    let id = Interlocked.Increment(&currentId)
    let requestedImage : MVar<ImageRequest> = MVar.empty()
    let renderTask = newRenderTask id createInfo
    let cancellationToken = app.CancellationToken

    [<VolatileField>]
    let mutable running = false

    let mutable frameCount = 0
    let roundTripTime = Stopwatch()
    let invalidateTime = Stopwatch()

    let info =
        {
            id         = createInfo.id
            token      = Unchecked.defaultof<AdaptiveToken>
            signature  = createInfo.signature
            sceneName  = createInfo.sceneName
            quality    = createInfo.targetQuality
            state      = RenderState.identity
            size       = V2i.II
            time       = MicroTime.Now
            clearColor = C4f.Black
        }

    let handleConnectionError (message: string) (exn: Exception) =
        match ConnectionError.ofException exn with
        | ConnectionError.Canceled -> Report.Line(3, $"[Client] {id}: Stopping")
        | ConnectionError.Closed   -> Report.Line(3, $"[Client] {id}: Connection closed")
        | ConnectionError.Lost     -> Report.Line(3, $"[Client] {id}: Connection lost")
        | _                        -> Log.warn $"[Client] {id}: {message}: {exn.GetBaseException().Message}"

        let source = InternalErrorSource.Connection (createInfo.id.session, message)
        createInfo.server.onError source exn

    let sender = AdaptiveObject.Create()

    let subscription =
        sender.AddMarkingCallback(fun () ->
            invalidateTime.Start()

            try
                let data = Pickler.json.Pickle RenderClientMessage.Invalidate
                let task = createInfo.socket.Send(WebSocketOpCode.Text, data, cancellationToken)
                task.Wait()
            with exn ->
                handleConnectionError "Failed to send invalidate message" exn
                running <- false
        )

    let renderLoop() =
        Report.Line(3, $"[Client] {id}: Started render thread for {info.id.session}/{info.id.elementId}")

        while running && not cancellationToken.IsCancellationRequested do
            let request = MVar.take requestedImage

            if request.size.AllGreater 0 && running && not cancellationToken.IsCancellationRequested then
                lock app.UpdateLock (fun _ ->
                    sender.EvaluateAlways AdaptiveToken.Top (fun token ->
                        try
                            let info =
                                { info with
                                    token      = token
                                    size       = request.size
                                    time       = MicroTime.Now
                                    clearColor = c4f request.background
                                }
                                |> RenderClientInfo.withState createInfo.server.getState

                            let data = renderTask.Run(token, info)

                            match data with
                            | RenderResult.Jpeg data ->
                                try
                                    let task = createInfo.socket.Send(WebSocketOpCode.Binary, data, cancellationToken)
                                    task.Wait()
                                with exn ->
                                    handleConnectionError "Could not send render result" exn
                                    running <- false

                            | RenderResult.Png _ ->
                                raise <| NotSupportedException("PNG render control not supported.")

                            | RenderResult.Mapping img ->
                                let data = Pickler.json.Pickle img

                                try
                                    let task = createInfo.socket.Send(WebSocketOpCode.Text, data, cancellationToken)
                                    task.Wait()
                                with exn ->
                                    handleConnectionError "Could not send render result" exn
                                    running <- false

                        with exn ->
                            running <- false
                            Log.error $"[Client] {id}: Rendering faulted: {exn}"
                            let source = InternalErrorSource.Rendering (info.id.session, info.id.elementId)
                            createInfo.server.onError source exn
                    )
                )

                createInfo.server.rendered info

        Report.Line(3, $"[Client] {id}: Stopped render thread")

    let mutable renderThread = Thread(ThreadStart(renderLoop), IsBackground = true, Name = $"RenderClient ({createInfo.id.session})")

    member _.Info = info
    member _.FrameCount = frameCount
    member _.FrameTime = roundTripTime.MicroTime
    member _.InvalidateTime = invalidateTime.MicroTime
    member _.RenderTime = renderTask.RenderTime
    member _.CompressTime = renderTask.CompressTime

    member this.Run() =
        running <- true
        renderThread.Start()

        task {
            let mappingStatus = if createInfo.useMapping then "enabled" else "disabled"
            Log.line $"[Client] {id}: Running {info.sceneName}, mapping {mappingStatus}"
            let buffer = SocketBuffer(128)

            while running && not cancellationToken.IsCancellationRequested do
                try
                    buffer.Position <- 0

                    let! message = createInfo.socket.Receive(buffer, cancellationToken)
                    let data = buffer.Data

                    match message with
                    | WebSocketOpCode.Text when data.Count > 0 ->
                        if data.[0] = uint8 '#' then
                            do! createInfo.socket.SendPong cancellationToken
                        else
                            try
                                let msg = RenderServerMessage.fromJson data

                                match msg with
                                | RenderServerMessage.RequestImage request ->
                                    invalidateTime.Stop()
                                    roundTripTime.Start()
                                    MVar.put requestedImage request

                                | RenderServerMessage.Rendered ->
                                    roundTripTime.Stop()
                                    frameCount <- frameCount + 1

                            with exn ->
                                let str = Encoding.UTF8.TryGetString data
                                if notNull str then Log.error $"[Client] {id}: Failed to process message: {str}"
                                Log.error $"[Client] {id}: Error during message processing: {exn}"
                                let source = InternalErrorSource.MessageParsing (info.session, str)
                                createInfo.server.onError source exn

                    | WebSocketOpCode.Close ->
                        Report.Line(3, $"[Client] {id}: Socket closed")
                        do! createInfo.socket.Close cancellationToken
                        running <- false

                    | WebSocketOpCode.Ping ->
                        do! createInfo.socket.SendPong cancellationToken

                    | _ ->
                        Log.warn $"[Client] {id}: Unexpected message {message}"

                with exn ->
                    handleConnectionError "Render socket I/O failed" exn
                    running <- false

            running <- false
            MVar.put requestedImage ImageRequest.empty
            renderThread.Join()
            renderThread <- null
            renderTask.Dispose()
            subscription.Dispose()

            Log.line $"[Client] {id}: Stopped"
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal RenderServer =

    [<AutoOpen>]
    module private Utils =
        type RenderClientStatistics =
            {
                session         : Guid
                name            : string
                frameCount      : int
                invalidateTime  : float
                renderTime      : float
                compressTime    : float
                frameTime       : float
            }

        type RenderClient with
            member internal this.GetStatistics() =
                {
                    session        = this.Info.id.session
                    name           = this.Info.sceneName
                    frameCount     = this.FrameCount
                    invalidateTime = this.InvalidateTime.TotalSeconds
                    renderTime     = this.RenderTime.TotalSeconds
                    compressTime   = this.CompressTime.TotalSeconds
                    frameTime      = this.FrameTime.TotalSeconds
                }

    let toWebPart (http: IHttpBackend<'HttpContext, 'HttpHandler>) (app: IMutableApp) (server: RenderServer) =
        let (>=>) a b = http.compose a b

        let clientCount = new CountdownEvent(1)
        let clients     = Dictionary<RenderClientId, RenderClient>()
        let signatures  = Dictionary<int, IFramebufferSignature>()
        let scenes      = Dictionary<Scene * IFramebufferSignature, ConcreteScene>()

        let getSignature (samples: int) =
            use _ = signatures.Locked
            signatures.GetCreate(samples, fun samples ->
                server.runtime.CreateFramebufferSignature(
                    [
                        DefaultSemantic.Colors, TextureFormat.Rgba8
                        DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                    ],
                    samples
                )
            )

        let getScene signature name =
            let scene = server.getScene name
            use _ = scenes.Locked
            scenes.GetCreate((scene, signature), fun (scene, signature) ->
                new ConcreteScene(scene, signature)
            )

        let render (targetId: string) (socket: IWebSocket) (context: 'HttpContext) : Task =
            let r = http.getRequest context

            let sceneName =
                match r.QueryParam "scene" with
                | Some scene -> scene
                | _ -> targetId

            let samples =
                match r.QueryParam "samples" with
                | Some (Int samples) -> samples
                | _ -> 1

            let sessionId =
                match r.QueryParam "session" with
                | Some id -> Guid.Parse id
                | _ -> Guid.NewGuid()

            let useMapping =
                match r.QueryParam "mapped" with
                | Some "false" -> false
                | Some "true" -> true
                | _ -> false

            let quality =
                match r.QueryParam "quality" with
                | Some q ->
                    match Int32.TryParse q with
                    | true, v when v >= 1 && v <= 100 -> float v
                    | _ ->
                        Log.warn "[Server] Could not parse quality. Should be int of range [1,100]."
                        RenderQuality.full.quality
                | _ ->
                    RenderQuality.full.quality

            let clientId = { session = sessionId; elementId = targetId }

            let signature = getSignature samples

            let createInfo =
                {
                    id              = clientId
                    server          = server
                    socket          = socket
                    signature       = signature
                    sceneName       = sceneName
                    getScene        = getScene
                    useMapping      = useMapping
                    targetQuality   = { RenderQuality.full with quality = quality }
                }

            let client = RenderClient(app, createInfo)

            lock clients (fun _ ->
                clients.[clientId] <- client
            )

            task {
                clientCount.AddCount()

                try do! client.Run()
                finally
                    lock clients (fun _ ->
                        match clients.TryGetValue clientId with
                        | true, oldClient when oldClient = client -> clients.Remove clientId |> ignore
                        | _ -> ()
                    )

                    socket.Dispose()
                    clientCount.Signal() |> ignore
            }

        let statistics =
            http.request (fun r ->
                let clients =
                    lock clients (fun _ ->
                        match r.QueryParam "session", r.QueryParam "name" with
                        | Some (Guid sid), Some name ->
                            match clients.TryGetValue { session = sid; elementId = name } with
                            | true, client -> [| client |]
                            | _ -> [||]
                        | _ ->
                            clients.Values |> Seq.toArray
                    )

                let stats = clients |> Array.map _.GetStatistics() |> Array.filter (fun s -> s.frameCount > 0)
                http.json (Pickler.json.PickleToString stats)
            )

        let screenshot (sceneName: string) =
            http.request (fun r ->
                let samples =
                    match r.QueryParam "samples" with
                    | Some (Int s) -> s
                    | _ -> 1

                let signature = getSignature samples

                match r.QueryParam "w", r.QueryParam "h" with
                | Some (Int w), Some (Int h) when w > 0 && h > 0 ->
                    let clearColor =
                        match r.QueryParam "background" with // fmt: C4f.Parse("[1.0,2.0,0.2,0.2]")
                        | Some (C4f c) -> c
                        | Some bg ->
                            Log.warn "[Screenshot] Could not parse background color: %s (format should be e.g. [1.0,2.0,0.2,0.2])" bg
                            C4f.Black
                        | _ -> C4f.Black

                    let clientInfo =
                        {
                            id         = { session = Guid.Empty; elementId = "" }
                            token      = AdaptiveToken.Top
                            signature  = signature
                            sceneName  = sceneName
                            size       = V2i(w, h)
                            time       = MicroTime.Now
                            clearColor = clearColor
                            quality    = RenderQuality.full
                            state      = RenderState.identity
                        }
                        |> RenderClientInfo.withState server.getState

                    let respondOK (mime : string) (task : ClientRenderTask)  =
                        let data =
                            match task.Run(AdaptiveToken.Top, clientInfo) with
                            | RenderResult.Jpeg d -> d
                            | RenderResult.Png d -> d
                            | _ -> failwith "that was unexpected"

                        http.mimeType mime >=> http.ok data

                    match r.QueryParam "fmt" with
                    | Some "jpg" | None ->
                        use t = new JpegClientRenderTask(server.runtime, server.compressor, 0, getScene, RenderQuality.full) :> ClientRenderTask
                        t |> respondOK "image/jpeg"

                    | Some "png" ->
                        use t = new PngClientRenderTask(server.runtime, 0, getScene) :> ClientRenderTask
                        t |> respondOK "image/png"

                    | Some fmt ->
                        http.badRequest $"Format not supported: {fmt}"

                | _ ->
                    http.badRequest "No width / height specified"
            )

        app.Register {
            new IDisposable with
                member x.Dispose() =
                    clientCount.Signal() |> ignore
                    clientCount.Wait()
                    clientCount.Dispose()

                    lock signatures (fun _ ->
                        for KeyValue(_, s) in signatures do s.Dispose()
                        signatures.Clear()
                    )

                    lock scenes (fun _ ->
                       for KeyValue(_, s) in scenes do s.Dispose()
                       scenes.Clear()
                    )
        }

        http.choose [
            http.routef "/render/%s" (render >> http.handShake)
            http.route "/stats.json" >=> statistics
            http.routef "/screenshot/%s" screenshot
        ]