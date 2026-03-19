namespace Aardvark.UI

open System
open System.Collections.Concurrent
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
        content    : string -> Scene voption
        rendered   : RenderClientInfo -> unit
        getState   : RenderClientInfo -> RenderState voption
        compressor : JpegCompressor voption
    }

type internal RenderClientCreateInfo =
    {
        server        : RenderServer
        id            : RenderClientId
        sceneName     : string
        samples       : int
        socket        : IWebSocket
        useMapping    : bool
        getSignature  : int -> IFramebufferSignature
        targetQuality : RenderQuality
    }

    member inline this.runtime    = this.server.runtime
    member inline this.compressor = this.server.compressor

type internal RenderClient(app: IMutableApp,
                           createInfo: RenderClientCreateInfo,
                           getState: RenderClientInfo -> RenderState,
                           getContent: IFramebufferSignature -> string -> ConcreteScene) as this =
    static let mutable currentId = 0

    static let newRenderTask (id: int) (info: RenderClientCreateInfo) getContent =
        if info.useMapping then
            new MappedClientRenderTask(info.runtime, id, getContent) :> ClientRenderTask
        else
            new JpegClientRenderTask(info.runtime, info.compressor, id, getContent, info.targetQuality) :> ClientRenderTask

    let id = Interlocked.Increment(&currentId)
    let sender = AdaptiveObject.Create()
    let requestedImage : MVar<ImageRequest> = MVar.empty()
    let mutable createInfo = createInfo
    let mutable renderTask = newRenderTask id createInfo getContent
    let mutable running = false
    let mutable disposed = 0
    let cancellationToken = app.CancellationToken

    let mutable frameCount = 0
    let roundTripTime = Stopwatch()
    let invalidateTime = Stopwatch()

    let send (msg: RenderClientMessage) =
        let data = Pickler.json.Pickle msg
        createInfo.socket.Send(WebSocketOpCode.Text, data, true, cancellationToken)

    let sendSync (msg: RenderClientMessage) =
        try
            let task = send msg
            task.Wait()
        with
        | :? OperationCanceledException -> ()
        | exn ->
            Log.error $"[Client] {id}: Send of {msg} faulted (stopping): {exn}"
            this.Dispose()

    let subscribe() =
        sender.AddMarkingCallback(fun () ->
            invalidateTime.Start()
            sendSync RenderClientMessage.Invalidate
        )

    let info =
        {
            id         = createInfo.id
            token      = Unchecked.defaultof<AdaptiveToken>
            signature  = createInfo.getSignature createInfo.samples
            sceneName  = createInfo.sceneName
            samples    = createInfo.samples
            quality    = createInfo.targetQuality
            state      = RenderState.identity
            size       = V2i.II
            time       = MicroTime.Now
            clearColor = C4f.Black
        }

    let mutable subscription = subscribe()

    let renderLoop() =
        Report.Line(3, $"[Client] {id}: Started render thread for {info.id.session}/{info.id.elementId}")

        while running && not cancellationToken.IsCancellationRequested do
            let request = MVar.take requestedImage

            if request.size.AllGreater 0 && running && not cancellationToken.IsCancellationRequested then
                use _ = app.AcquireLock()

                sender.EvaluateAlways AdaptiveToken.Top (fun token ->
                    try
                        let info =
                            { info with
                                token      = token
                                size       = request.size
                                time       = MicroTime.Now
                                clearColor = c4f request.background
                            }
                            |> RenderClientInfo.withState getState

                        let data = renderTask.Run(token, info)

                        match data with
                        | RenderResult.Jpeg data ->
                            try
                                let task = createInfo.socket.Send(WebSocketOpCode.Binary, data, true, cancellationToken)
                                task.Wait()
                            with
                            | :? OperationCanceledException -> ()
                            | exn ->
                                running <- false
                                Log.error $"[Client] {id}: Could not send render result (stopping): {exn}"

                        | RenderResult.Png _ ->
                            Log.error $"[Client] {id}: Requested png render control which is not supported at the moment (png conversion too slow)"

                        | RenderResult.Mapping img ->
                            let data = Pickler.json.Pickle img

                            try
                                let task = createInfo.socket.Send(WebSocketOpCode.Text, data, true, cancellationToken)
                                task.Wait()
                            with
                            | :? OperationCanceledException -> ()
                            | exn ->
                                running <- false
                                Log.error $"[Client] {id}: Could not send render result (stopping): {exn}"

                    with exn ->
                        running <- false
                        Log.error $"[Client] {id}: Rendering faulted (stopping): {exn}"
                )

        Report.Line(3, $"[Client] {id}: Stopped render thread for {info.id.session}/{info.id.elementId}")

    let mutable renderThread = Thread(ThreadStart(renderLoop), IsBackground = true, Name = $"RenderClient ({createInfo.id.session})")

    member _.Info = info
    member _.FrameCount = frameCount
    member _.FrameTime = roundTripTime.MicroTime
    member _.InvalidateTime = invalidateTime.MicroTime
    member _.RenderTime = renderTask.RenderTime
    member _.CompressTime = renderTask.CompressTime

    member _.Revive(newInfo : RenderClientCreateInfo) =
        if Interlocked.Exchange(&disposed, 0) = 1 then
            Log.line $"[Client] {id}: Revived"
            createInfo <- newInfo
            renderTask <- newRenderTask id newInfo getContent
            subscription <- subscribe()
            renderThread <- Thread(ThreadStart(renderLoop), IsBackground = true, Name = $"RenderClient ({createInfo.id.session})")

    member _.Dispose() =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            running <- false
            MVar.put requestedImage ImageRequest.empty
            renderThread.Join()
            renderThread <- null
            renderTask.Dispose()
            subscription.Dispose()
            frameCount <- 0
            roundTripTime.Reset()
            invalidateTime.Reset()
            createInfo.socket.Dispose()

    member this.Run =
        running <- true
        renderThread.Start()

        task {
            Log.line $"[Client] {id}: Running {info.sceneName}"
            let buffer = SocketBuffer(128)

            while running && not cancellationToken.IsCancellationRequested do
                try
                    buffer.Position <- 0

                    let! message = createInfo.socket.Receive(buffer, cancellationToken)
                    let data = buffer.Data

                    match message with
                    | WebSocketOpCode.Text when data.Count > 0 ->
                        try
                            if data.[0] = uint8 '#' then
                                do! createInfo.socket.SendPong cancellationToken
                            else
                                let msg = RenderServerMessage.fromJson data

                                match msg with
                                | RenderServerMessage.RequestImage request ->
                                    invalidateTime.Stop()
                                    roundTripTime.Start()
                                    MVar.put requestedImage request

                                | RenderServerMessage.RequestWorldPosition pixel ->
                                    let wp =
                                        match renderTask.GetWorldPosition pixel with
                                        | ValueSome d -> d
                                        | ValueNone -> V3d.Zero

                                    try
                                        do! send (RenderClientMessage.WorldPosition wp)
                                    with
                                    | :? OperationCanceledException -> ()
                                    | exn ->
                                        running <- false
                                        Log.error $"[Client] {id}: Could not send world position (stopping): {exn}"

                                | RenderServerMessage.Rendered ->
                                    roundTripTime.Stop()
                                    frameCount <- frameCount + 1

                        with exn ->
                            let str = Encoding.UTF8.TryGetString data
                            if notNull str then Log.error $"[Client] {id}: Failed to process message: {str}"
                            Log.error $"[Client] {id}: Error during message processing: {exn}"

                    | WebSocketOpCode.Close ->
                        running <- false

                    | WebSocketOpCode.Ping ->
                        do! createInfo.socket.SendPong cancellationToken

                    | _ ->
                        Log.warn $"[Client] {id}: Unexpected message {message}"

                with
                | :? OperationCanceledException ->
                    running <- false

                | exn ->
                    Log.error $"[Client] {id}: Socket I/O failed: {exn}"

            this.Dispose()
            Log.line $"[Client] {id}: Stopped"
        }

    interface IDisposable with
        member this.Dispose() = this.Dispose()

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

    let empty (useGpuCompression: bool) (runtime: IRuntime) =
        let compressor =
            if useGpuCompression then new JpegCompressor(runtime) |> ValueSome
            else ValueNone

        {
            runtime        = runtime
            rendered       = fun _ -> ()
            content        = fun _ -> ValueNone
            getState       = fun _ -> ValueNone
            compressor     = compressor
        }

    let withState (getState: RenderClientInfo -> RenderState) (server: RenderServer) =
        { server with getState = getState >> ValueSome }

    let withContent (getContent: string -> Scene voption) (server: RenderServer) =
        { server with content = getContent }

    let addScenes (findScene: string -> Scene voption) (server: RenderServer) =
        { server with content = fun n -> findScene n |> ValueOption.orElseWith (fun () -> server.content n) }

    let addScene (name: string) (scene: Scene) (server: RenderServer) =
        { server with content = fun n -> if n = name then ValueSome scene else server.content n }

    let add (name: string) (create: RenderClientValues -> IRenderTask) (server: RenderServer) =
        addScene name (Scene.custom create) server

    let toWebPart (http: IHttpBackend<'HttpContext, 'HttpHandler>) (app: IMutableApp) (server: RenderServer) =
        let (>=>) a b = http.compose a b

        let clients = ConcurrentDictionary<RenderClientId, Lazy<RenderClient>>()
        let signatures = ConcurrentDictionary<int, Lazy<IFramebufferSignature>>()

        let getSignature (samples: int) =
            signatures.GetOrAdd(samples, fun samples ->
                lazy server.runtime.CreateFramebufferSignature(
                     [
                         DefaultSemantic.Colors, TextureFormat.Rgba8
                         DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                     ],
                     samples
                )
            ).Value

        let getState =
            server.getState >> ValueOption.defaultValue RenderState.identity

        let content signature name  =
            match server.content name with
            | ValueSome scene -> scene.GetConcreteScene(signature)
            | _ -> Scene.empty.GetConcreteScene(signature)

        let render (targetId: string) (socket: IWebSocket) (context: 'HttpContext) : Task =
            let args = http.requestQueryParams context

            let sceneName =
                match Map.tryFindV "scene" args with
                | ValueSome scene -> scene
                | _ -> targetId

            let samples =
                match Map.tryFindV "samples" args with
                | ValueSome (Int samples) -> samples
                | _ -> 1

            let sessionId =
                match Map.tryFindV "session" args with
                | ValueSome id -> Guid.Parse id
                | _ -> Guid.NewGuid()

            let useMapping =
                match Map.tryFindV "mapped" args with
                | ValueSome "false" -> false
                | ValueSome "true" -> true
                | _ -> false

            let quality =
                match Map.tryFindV "quality" args with
                | ValueSome q ->
                    match Int32.TryParse q with
                    | true, v when v >= 1 && v <= 100 -> float v
                    | _ ->
                        Log.warn "[Server] Could not parse quality. Should be int of range [1,100]."
                        RenderQuality.full.quality
                | _ ->
                    RenderQuality.full.quality

            let clientId = { session = sessionId; elementId = targetId }

            let createInfo =
                {
                    server          = server
                    id              = clientId
                    sceneName       = sceneName
                    samples         = samples
                    targetQuality   = { RenderQuality.full with quality = quality }
                    socket          = socket
                    useMapping      = useMapping
                    getSignature    = getSignature
                }

            let client =
                clients.GetOrAdd(clientId, fun _ ->
                    lazy (
                        Log.line "[Server] Created client for %A/%s, mapping %s" sessionId targetId (if useMapping then "enabled" else "disabled")
                        new RenderClient(app, createInfo, getState, content)
                    )
                ).Value

            client.Revive(createInfo)
            client.Run

        let statistics =
            http.withContext (fun context ->
                let args = http.requestQueryParams context

                let clients =
                    match Map.tryFindV "session" args, Map.tryFindV "name" args with
                    | ValueSome (Guid sid), ValueSome name ->
                        match clients.TryGetValue { session = sid; elementId = name } with
                        | true, c -> [| c.Value |]
                        | _ -> [||]
                    | _ ->
                        clients.Values |> Seq.map _.Value |> Seq.toArray

                let stats = clients |> Array.map _.GetStatistics() |> Array.filter (fun s -> s.frameCount > 0)
                let json = Pickler.json.PickleToString stats

                http.ok json >=> http.mimeType "text/json"
            )

        let screenshot (sceneName: string) =
            http.withContext (fun context ->
                let args = http.requestQueryParams context

                let samples =
                    match Map.tryFindV "samples" args with
                    | ValueSome (Int s) -> s
                    | _ -> 1

                let signature = getSignature samples

                match Map.tryFindV "w" args, Map.tryFindV "h" args with
                | ValueSome (Int w), ValueSome (Int h) when w > 0 && h > 0 ->
                    //let scene = content signature sceneName

                    let clearColor =
                        match Map.tryFindV "background" args with // fmt: C4f.Parse("[1.0,2.0,0.2,0.2]")
                        | ValueSome (C4f c) -> c
                        | ValueSome bg ->
                            Log.warn "[Screenshot] Could not parse background color: %s (format should be e.g. [1.0,2.0,0.2,0.2])" bg
                            C4f.Black
                        | ValueNone -> C4f.Black

                    let clientInfo =
                        {
                            id         = { session = Guid.Empty; elementId = "" }
                            token      = AdaptiveToken.Top
                            signature  = signature
                            sceneName  = sceneName
                            size       = V2i(w, h)
                            samples    = samples
                            time       = MicroTime.Now
                            clearColor = clearColor
                            quality    = RenderQuality.full
                            state      = RenderState.identity
                        }
                        |> RenderClientInfo.withState getState

                    let respondOK (mime : string) (task : ClientRenderTask)  =
                        let data =
                            match task.Run(AdaptiveToken.Top, clientInfo) with
                            | RenderResult.Jpeg d -> d
                            | RenderResult.Png d -> d
                            | _ -> failwith "that was unexpected"

                        http.ok data >=> http.mimeType mime

                    match Map.tryFindV "fmt" args with
                    | ValueSome "jpg" | ValueNone ->
                        use t = new JpegClientRenderTask(server.runtime, server.compressor, 0, content, RenderQuality.full) :> ClientRenderTask
                        t |> respondOK "image/jpeg"

                    | ValueSome "png" ->
                        use t = new PngClientRenderTask(server.runtime, 0, content) :> ClientRenderTask
                        t |> respondOK "image/png"

                    | ValueSome fmt ->
                        http.badRequest $"Format not supported: {fmt}"

                | _ ->
                    http.badRequest "No width / height specified"
            )

        http.choose [
            http.routef "/render/%s" (render >> http.handShake)
            http.route "/stats.json" >=> statistics
            http.routef "/screenshot/%s" screenshot
        ]