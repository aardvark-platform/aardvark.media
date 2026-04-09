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
            running <- false

    let sender = AdaptiveObject.Create()

    let subscription =
        sender.AddMarkingCallback(fun () ->
            invalidateTime.Start()
            sendSync RenderClientMessage.Invalidate
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
                )

                createInfo.server.rendered info

        Report.Line(3, $"[Client] {id}: Stopped render thread for {info.id.session}/{info.id.elementId}")

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
            http.withContext (fun context ->
                let args = http.requestQueryParams context

                let clients =
                    lock clients (fun _ ->
                        match Map.tryFindV "session" args, Map.tryFindV "name" args with
                        | ValueSome (Guid sid), ValueSome name ->
                            match clients.TryGetValue { session = sid; elementId = name } with
                            | true, client -> [| client |]
                            | _ -> [||]
                        | _ ->
                            clients.Values |> Seq.toArray
                    )

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

                        http.ok data >=> http.mimeType mime

                    match Map.tryFindV "fmt" args with
                    | ValueSome "jpg" | ValueNone ->
                        use t = new JpegClientRenderTask(server.runtime, server.compressor, 0, getScene, RenderQuality.full) :> ClientRenderTask
                        t |> respondOK "image/jpeg"

                    | ValueSome "png" ->
                        use t = new PngClientRenderTask(server.runtime, 0, getScene) :> ClientRenderTask
                        t |> respondOK "image/png"

                    | ValueSome fmt ->
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