namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Text
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``RenderClient Tests`` =

    let private timeout = TimeSpan.FromSeconds 5.0
    let private wait (work: Task<'T>) = work.WaitAsync(timeout).GetAwaiter().GetResult()

    type private Fault = None | State | Render | Png

    type private Send =
        { kind: WebSocketOpCode
          data: byte[]
          token: CancellationToken
          final: bool
          locked: bool
          completion: TaskCompletionSource<unit> }

    type private Fixture(mapped: bool, fault: Fault) =
        let updateLock = obj()
        let cancellation = new CancellationTokenSource()
        let incoming = System.Threading.Channels.Channel.CreateUnbounded<WebSocketOpCode * byte[]>()
        let receives = System.Threading.Channels.Channel.CreateUnbounded<unit>()
        let sends = System.Threading.Channels.Channel.CreateUnbounded<Send>()
        let posts = System.Threading.Channels.Channel.CreateUnbounded<RenderClientInfo * bool>()
        let pending = ConcurrentQueue<Send>()
        let events = ConcurrentQueue<string>()
        let frames = ConcurrentQueue<RenderClientInfo * AdaptiveToken * bool>()
        let notifications = ConcurrentQueue<RenderClientInfo>()
        let errors = ConcurrentQueue<InternalErrorSource * exn>()
        let failure = InvalidOperationException("Injected frame failure")
        let times = [| MicroTime(17L); MicroTime(123456L); MicroTime(987654L) |]
        let states = [|
            { viewTrafo = Trafo3d.Translation(1.0, 2.0, 3.0); projTrafo = Trafo3d.Scale(2.0, 3.0, 4.0) }
            { viewTrafo = Trafo3d.Translation(-4.0, 5.0, 6.0); projTrafo = Trafo3d.Scale(7.0, 8.0, 9.0) }
        |]
        let jpeg = [| [| 255uy; 216uy; 1uy; 255uy; 217uy |]; [| 255uy; 216uy; 2uy; 255uy; 217uy |] |]
        let mutable clockReads = 0
        let mutable stateReads = 0
        let mutable closes = 0
        let signature =
            { new IFramebufferSignature with
                member _.Runtime = Unchecked.defaultof<_>
                member _.Samples = 4
                member _.ColorAttachments = Map.empty
                member _.DepthStencilAttachment = Option.None
                member _.LayerCount = 1
                member _.PerLayerUniforms = Set.empty
                member _.Dispose() = failwith "The client does not own its signature" }
        let socket =
            { new IWebSocket with
                member _.Send(kind, data, token, final) =
                    events.Enqueue "send"
                    let send = { kind = kind; data = data; token = token; final = final
                                 locked = Monitor.IsEntered updateLock
                                 completion = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously) }
                    pending.Enqueue send
                    sends.Writer.TryWrite send |> ignore
                    send.completion.Task :> Task
                member _.Receive(buffer, token) =
                    task {
                        receives.Writer.TryWrite(()) |> ignore
                        let! kind, data = incoming.Reader.ReadAsync(token)
                        buffer.Write data
                        return kind
                    }
                member _.Close _ =
                    Interlocked.Increment(&closes) |> ignore
                    Task.CompletedTask
                member _.Dispose() = () }
        let app =
            { new IMutableApp with
                member _.UpdateLock = updateLock
                member _.CancellationToken = cancellation.Token
                member _.Register _ = failwith "Unexpected resource registration"
                member _.Dispose() = () }
        let createInfo =
            { id = { session = Guid.Parse "8173a338-1183-434d-a813-a339f5c1c678"; elementId = "viewport" }
              server =
                { runtime = Unchecked.defaultof<_>
                  getScene = fun _ -> failwith "No GPU scene is used"
                  getState = fun _ ->
                    events.Enqueue "state"
                    if not (Monitor.IsEntered updateLock) then failwith "State read outside update lock"
                    let index = Interlocked.Increment(&stateReads) - 1
                    if fault = State then raise failure
                    states.[index]
                  rendered = fun info ->
                    events.Enqueue "post"
                    notifications.Enqueue info
                    posts.Writer.TryWrite(info, Monitor.IsEntered updateLock) |> ignore
                  compressor = ValueNone
                  onError = fun source error ->
                    events.Enqueue "error"
                    errors.Enqueue(source, error) }
              socket = socket
              signature = signature
              sceneName = "distinct-scene"
              getScene = fun _ _ -> failwith "No GPU scene is used"
              useMapping = mapped
              targetQuality = { quality = 73.0; scale = 0.75 } }
        let client =
            RenderClient(app, createInfo,
                getTime = (fun () -> times.[Interlocked.Increment(&clockReads) - 1]),
                render = (fun (token, info) ->
                    events.Enqueue "render"
                    frames.Enqueue(info, token, Monitor.IsEntered updateLock)
                    if fault = Render then raise failure
                    if fault = Png then RenderResult.Png [| 1uy |]
                    elif mapped then RenderResult.Mapping { name = "cpu-frame"; size = info.size; length = 32768 }
                    else RenderResult.Jpeg jpeg.[frames.Count - 1]))
        let work = client.Run()

        member _.Client = client
        member _.CreateInfo = createInfo
        member _.Times = times
        member _.States = states
        member _.Jpeg = jpeg
        member _.Frames = frames.ToArray()
        member _.Posts = notifications.ToArray()
        member _.Events = events.ToArray() |> Array.toList
        member _.Errors = errors.ToArray()
        member _.Failure = failure
        member _.Token = cancellation.Token
        member _.ClockReads = Volatile.Read(&clockReads)
        member _.StateReads = Volatile.Read(&stateReads)
        member _.Closes = Volatile.Read(&closes)
        member _.NextReceive() = receives.Reader.ReadAsync().AsTask()
        member _.NextSend() = sends.Reader.ReadAsync().AsTask()
        member _.NextPost() = posts.Reader.ReadAsync().AsTask()
        member _.Cancel() = cancellation.Cancel()
        member _.Request(size: V2i, color: C4b) =
            let json = sprintf "{\"case\":\"RequestImage\",\"size\":{\"X\":%d,\"Y\":%d},\"background\":{\"R\":%d,\"G\":%d,\"B\":%d,\"A\":%d}}"
                               size.X size.Y color.R color.G color.B color.A
            incoming.Writer.TryWrite(WebSocketOpCode.Text, Encoding.UTF8.GetBytes json) |> ignore
        member _.Close() = incoming.Writer.TryWrite(WebSocketOpCode.Close, Array.empty) |> ignore
        member _.Join() = work.WaitAsync(timeout).GetAwaiter().GetResult()

        interface IDisposable with
            member _.Dispose() =
                cancellation.Cancel()
                for send in pending do send.completion.TrySetCanceled(cancellation.Token) |> ignore
                try work.WaitAsync(timeout).GetAwaiter().GetResult()
                finally cancellation.Dispose()

    let private checkSnapshot (fixture: Fixture) index (posted: RenderClientInfo, locked) =
        let rendered, token, renderLocked = fixture.Frames.[index]
        Expect.isTrue (Object.ReferenceEquals(posted, rendered)) "Post callback receives the exact render snapshot"
        Expect.isTrue renderLocked "Render remains inside the app update lock"
        Expect.isFalse locked "Post callback remains outside the app update lock"
        Expect.equal posted.token token "Preserve the adaptive evaluation token"
        Expect.equal posted.time fixture.Times.[index + 1] "Use the frame timestamp, not construction time"
        Expect.equal posted.state fixture.States.[index] "Preserve nonidentity view and projection transforms"
        Expect.isTrue (Object.ReferenceEquals(posted.id, fixture.CreateInfo.id)) "Preserve client/session identity"
        Expect.isTrue (Object.ReferenceEquals(posted.signature, fixture.CreateInfo.signature)) "Preserve signature identity"
        Expect.equal posted.sceneName "distinct-scene" "Preserve scene"
        Expect.isTrue (Object.ReferenceEquals(posted.quality, fixture.CreateInfo.targetQuality)) "Preserve quality"
        posted

    let private consecutiveFrames mapped =
        use fixture = new Fixture(mapped, None)
        let requests = [|
            V2i(640, 480), C4b(255uy, 0uy, 0uy, 255uy), C4f(1.0f, 0.0f, 0.0f, 1.0f)
            V2i(913, 277), C4b(0uy, 128uy, 255uy, 64uy), C4f(0.0f, 128.0f / 255.0f, 1.0f, 64.0f / 255.0f)
        |]
        let postedFrames = ResizeArray<RenderClientInfo * bool>()
        for index in 0 .. requests.Length - 1 do
            let size, background, _ = requests.[index]
            fixture.Request(size, background)
            let send = wait (fixture.NextSend())
            Expect.isTrue send.locked "Send remains inside the update lock"
            Expect.equal send.token fixture.Token "Forward app cancellation"
            Expect.isTrue send.final "Preserve complete-frame sends"
            Expect.equal fixture.Posts.Length index "No post callback before send completion"
            if mapped then
                Expect.equal send.kind WebSocketOpCode.Text "Mapping metadata remains a text frame"
                let image = Pickler.json.UnPickle<MappedImage> send.data
                Expect.equal image { name = "cpu-frame"; size = size; length = 32768 } "Preserve mapped payload"
            else
                Expect.equal send.kind WebSocketOpCode.Binary "JPEG remains a binary frame"
                Expect.isTrue (Object.ReferenceEquals(send.data, fixture.Jpeg.[index])) "Forward JPEG bytes unchanged"
            send.completion.SetResult(())
            postedFrames.Add(wait (fixture.NextPost()))
        fixture.Close()
        fixture.Join()
        Expect.equal fixture.Events ["state"; "render"; "send"; "post"; "state"; "render"; "send"; "post"] "Preserve render/send/post order"
        Expect.isEmpty fixture.Errors "Normal close must not report errors"
        Expect.equal fixture.Closes 1 "Close the socket once"
        Expect.equal (fixture.ClockReads, fixture.StateReads) (3, 2) "No extra frame on shutdown"
        Expect.equal fixture.Client.Info.size V2i.II "Do not replace construction-time client info"
        Expect.equal fixture.Client.Info.time fixture.Times.[0] "Keep initial client info immutable"
        let first, _, _ = fixture.Frames.[0]
        Expect.equal first.size (V2i(640, 480)) "Later frames do not mutate earlier snapshots"
        for index in 0 .. requests.Length - 1 do
            let size, _, expectedBackground = requests.[index]
            let posted = checkSnapshot fixture index postedFrames.[index]
            Expect.equal posted.size size "Use the requested viewport"
            Expect.equal posted.clearColor expectedBackground "Use the requested clear color"

    [<Tests>]
    let tests =
        testList "RenderClient Tests" [
            testCase "JPEG frame metadata and handoff" <| fun _ -> consecutiveFrames false
            testCase "Mapped frame metadata and handoff" <| fun _ -> consecutiveFrames true

            testCase "State failures retain fallback notification" <| fun _ ->
                use fixture = new Fixture(false, State)
                fixture.Request(V2i(640, 480), C4b.Red)
                let posted, locked = wait (fixture.NextPost())
                fixture.Close()
                fixture.Join()
                Expect.isTrue (Object.ReferenceEquals(posted, fixture.Client.Info)) "No render snapshot exists when state creation fails"
                Expect.isFalse locked "Fallback notification is outside the lock"
                Expect.isEmpty fixture.Frames "Do not render after state failure"
                Expect.equal fixture.Events ["state"; "error"; "post"] "Keep failure reporting before notification"
                let source, error = Array.exactlyOne fixture.Errors
                Expect.equal source (InternalErrorSource.Rendering(fixture.CreateInfo.id.session, "viewport")) "Preserve error classification"
                Expect.isTrue (Object.ReferenceEquals(error, fixture.Failure)) "Preserve exception identity"

            testCase "Render faults and unsupported PNG still notify" <| fun _ ->
                for fault in [Render; Png] do
                    use fixture = new Fixture(false, fault)
                    fixture.Request(V2i(640, 480), C4b.Red)
                    let posted = wait (fixture.NextPost())
                    fixture.Close()
                    fixture.Join()
                    Expect.equal fixture.Events ["state"; "render"; "error"; "post"] "No send after render failure"
                    let source, error = Array.exactlyOne fixture.Errors
                    Expect.equal source (InternalErrorSource.Rendering(fixture.CreateInfo.id.session, "viewport")) "Preserve rendering error source"
                    if fault = Render then
                        Expect.isTrue (Object.ReferenceEquals(error, fixture.Failure)) "Preserve render exception identity"
                    else
                        Expect.isTrue (error :? NotSupportedException) "PNG remains unsupported"
                    checkSnapshot fixture 0 posted |> ignore

            testCase "Send faults still notify and stop" <| fun _ ->
                for mapped in [false; true] do
                    use fixture = new Fixture(mapped, None)
                    fixture.Request(V2i(640, 480), C4b.Red)
                    let send = wait (fixture.NextSend())
                    send.completion.SetException fixture.Failure
                    let posted = wait (fixture.NextPost())
                    fixture.Close()
                    fixture.Join()
                    Expect.equal fixture.Events ["state"; "render"; "send"; "error"; "post"] "Report send failure before post"
                    let source, error = Array.exactlyOne fixture.Errors
                    Expect.equal source (InternalErrorSource.Connection(fixture.CreateInfo.id.session, "Could not send render result")) "Preserve connection error source"
                    Expect.isTrue (Object.ReferenceEquals(error.GetBaseException(), fixture.Failure)) "Retain Task.Wait error wrapping"
                    checkSnapshot fixture 0 posted |> ignore

            testCase "Cancellation and invalid requests keep shutdown behavior" <| fun _ ->
                use idle = new Fixture(false, None)
                idle.Request(V2i.Zero, C4b.Red)
                idle.Request(V2i(-1, 480), C4b.Blue)
                for _ in 1 .. 3 do wait (idle.NextReceive())
                idle.Cancel()
                idle.Join()
                Expect.isEmpty idle.Frames "Invalid or canceled requests do not render"
                Expect.isEmpty idle.Posts "No post callback without an attempted frame"
                Expect.equal idle.ClockReads 1 "No frame timestamp read after cancellation"
                Expect.equal idle.Closes 0 "Cancellation does not send Close"
                let source, error = Array.exactlyOne idle.Errors
                Expect.equal source (InternalErrorSource.Connection(idle.CreateInfo.id.session, "Render socket I/O failed")) "Preserve receive cancellation reporting"
                Expect.isTrue (error :? OperationCanceledException) "Preserve cancellation exception"

                for mapped in [false; true] do
                    use fixture = new Fixture(mapped, None)
                    fixture.Request(V2i(640, 480), C4b.Red)
                    let send = wait (fixture.NextSend())
                    for _ in 1 .. 2 do wait (fixture.NextReceive())
                    fixture.Cancel()
                    send.completion.TrySetCanceled(fixture.Token) |> ignore
                    let posted = wait (fixture.NextPost())
                    fixture.Join()
                    Expect.equal fixture.Posts.Length 1 "A canceled send retains one post callback"
                    Expect.equal (fixture.Events |> List.filter (fun e -> e <> "error")) ["state"; "render"; "send"; "post"] "Preserve attempted-frame order during cancellation"
                    Expect.equal fixture.Errors.Length 2 "Retain send and receive cancellation reporting"
                    for _, error in fixture.Errors do
                        Expect.isTrue (error.GetBaseException() :? OperationCanceledException) "Do not change cancellation errors"
                    checkSnapshot fixture 0 posted |> ignore
        ]
