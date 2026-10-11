namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Runtime.ExceptionServices
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Aardvark.UI
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``MutableApp Channel Tests`` =

    let private timeout = TimeSpan.FromSeconds 5.0

    type private RecordingReader(name: string, value: cval<int>, failRelease: bool) =
        inherit ChannelReader()
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let gate = new ManualResetEventSlim(false)
        let mutable releases = 0
        let mutable detachedAtRelease = false

        member _.Entered = entered.Task
        member _.Releases = Volatile.Read(&releases)
        member _.DetachedAtRelease = detachedAtRelease
        member _.AllowEvaluation() = gate.Set()
        member _.DisposeGate() = gate.Dispose()

        override _.ComputeMessages token =
            let current = value.GetValue token
            entered.TrySetResult(()) |> ignore
            if not (gate.Wait timeout) then failwith "Reader evaluation gate timed out"
            [JsonSerializer.Serialize($"{name}:{current}")]

        override this.Release() =
            Interlocked.Increment(&releases) |> ignore
            detachedAtRelease <- this.Outputs.IsEmpty
            if failRelease then failwith $"Release failed for {name}"

    type private RecordingChannel(name: string, failRelease: bool) =
        inherit Aardvark.UI.Channel()
        let value = AVal.init 0
        let created = System.Threading.Channels.Channel.CreateUnbounded<RecordingReader>()
        let readers = ConcurrentQueue<RecordingReader>()

        member _.Name = name
        member _.Readers = readers.ToArray()
        member _.NextReader() = created.Reader.ReadAsync().AsTask().WaitAsync timeout
        member _.ChangeValue next = transact (fun () -> value.Value <- next)

        override _.GetReader() =
            let reader = new RecordingReader(name, value, failRelease)
            readers.Enqueue reader
            created.Writer.TryWrite reader |> ignore
            reader :> ChannelReader

    type private EndReason = NormalClose | ReceiveFailure | AppCancellation

    type private SessionSocket() =
        inherit System.Net.WebSockets.WebSocket()
        let ending = TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        let receiveEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let receiveEnded = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let batches = System.Threading.Channels.Channel.CreateUnbounded<string>()
        let failure = InvalidOperationException("Injected receive failure")
        let mutable closes = 0

        member _.ReceiveEntered = receiveEntered.Task
        member _.ReceiveEnded = receiveEnded.Task
        member _.Failure = failure
        member _.Closes = Volatile.Read(&closes)
        member _.End(fail) = ending.TrySetResult fail |> ignore
        member _.NextBatch() = batches.Reader.ReadAsync().AsTask().WaitAsync timeout

        override _.State = WebSocketState.Open
        override _.CloseStatus = Nullable<WebSocketCloseStatus>()
        override _.CloseStatusDescription = null
        override _.SubProtocol = null
        override this.Abort() = this.End false
        override this.Dispose() = this.End false
        override _.CloseOutputAsync(_, _, _) = failwith "Unexpected CloseOutputAsync"

        override _.ReceiveAsync(_: ArraySegment<byte>, token: CancellationToken) =
            task {
                receiveEntered.TrySetResult(()) |> ignore
                try
                    let! fail = ending.Task.WaitAsync token
                    if fail then raise failure
                    return WebSocketReceiveResult(0, WebSocketMessageType.Close, true)
                finally
                    receiveEnded.TrySetResult(()) |> ignore
            }

        override _.SendAsync(data: ArraySegment<byte>, kind, final, token: CancellationToken) =
            token.ThrowIfCancellationRequested()
            Expect.equal (kind, final) (WebSocketMessageType.Text, true) "Channel/DOM frames must retain their transport encoding"
            let frame = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count)
            if frame.StartsWith "{" then batches.Writer.TryWrite frame |> ignore
            Task.CompletedTask

        override _.CloseAsync(status, description, _) =
            Expect.equal (status, description) (WebSocketCloseStatus.NormalClosure, null) "Normal close arguments must be unchanged"
            Interlocked.Increment(&closes) |> ignore
            Task.CompletedTask

    type private Session =
        { socket: SessionSocket; work: Task<HttpContext option>; readers: RecordingReader list }

    type private Fixture(channels: RecordingChannel list) =
        let nodes =
            channels |> List.map (fun channel ->
                channel.Name,
                (Incremental.elem "div" AttributeMap.empty AList.empty
                 |> onBoot' ["stream", channel :> Aardvark.UI.Channel] "")
            )
        let app =
            {
                initial = true
                unpersist = { create = AVal.init; update = fun model value -> model.Value <- value }
                threads = fun _ -> ThreadPool.empty
                update = fun _ visible -> visible
                view = fun visible ->
                    let children =
                        visible |> AVal.map (fun visible ->
                            nodes |> List.choose (fun (name, node) -> if name = "removed" && not visible then None else Some node)
                        ) |> AList.ofAVal
                    Incremental.elem "div" AttributeMap.empty children
            } |> App.start
        // No render nodes, render requests, GPU compression or runtime operations are used.
        let handler = MutableApp.toWebPart Giraffe.HttpBackend.Instance Unchecked.defaultof<IRuntime> app
        let sessions = ResizeArray<SessionSocket * Task<HttpContext option>>()
        let errors = ConcurrentQueue<InternalErrorEventArgs>()
        let errorSubscription = app.InternalError.Subscribe errors.Enqueue
        let mutable disposal = None

        member _.App = app
        member _.Errors = errors.ToArray()
        member _.DisposeApp() =
            match disposal with
            | Some task -> task
            | None ->
                let task = Task.Run(fun () -> app.Dispose())
                disposal <- Some task
                task

        member _.StartSession() =
            task {
                let socket = new SessionSocket()
                let context = DefaultHttpContext()
                context.Request.Method <- "GET"
                context.Request.Path <- PathString "/events"
                context.Request.QueryString <- QueryString $"?session={Guid.NewGuid()}"
                context.Features.Set<IHttpWebSocketFeature>(
                    { new IHttpWebSocketFeature with
                        member _.IsWebSocketRequest = true
                        member _.AcceptAsync _ = Task.FromResult(socket :> System.Net.WebSockets.WebSocket) }
                )
                let work = handler (Some >> Task.FromResult) context
                sessions.Add(socket, work)
                do! socket.ReceiveEntered.WaitAsync timeout
                let! readers = channels |> List.map (fun channel -> channel.NextReader()) |> Task.WhenAll
                return { socket = socket; work = work; readers = Array.toList readers }
            }

        member this.Cleanup() =
            for channel in channels do
                for reader in channel.Readers do reader.AllowEvaluation()
            for socket, _ in sessions do socket.End false
            let pending = sessions |> Seq.map snd |> Seq.toArray
            task {
                for work in pending do
                    try do! (work.WaitAsync timeout :> Task)
                    with error ->
                        if not work.IsCompleted then raise error
                // Drain sessions/update workers before disposing any fixture gates or sockets.
                try do! this.DisposeApp().WaitAsync timeout
                finally
                    errorSubscription.Dispose()
                    for socket, _ in sessions do socket.Dispose()
                    for channel in channels do
                        for reader in channel.Readers do
                            // Observe app-owned release before this test-owned fallback.
                            if reader.Releases = 0 then
                                try reader.Dispose() with _ -> ()
                            reader.Outputs.Clear()
                            reader.DisposeGate()
            }

    let private withFixture channels action =
        task {
            let fixture = Fixture(channels)
            let mutable failure = None
            try do! action fixture
            with error -> failure <- Some (ExceptionDispatchInfo.Capture error)
            do! fixture.Cleanup()
            failure |> Option.iter (fun error -> error.Throw())
        }

    let private allowInitialEvaluation (session: Session) =
        task {
            let! _ = (session.readers |> List.map (fun reader -> reader.Entered) |> Task.WhenAny).WaitAsync timeout
            for reader in session.readers do reader.AllowEvaluation()
            let! _ = (session.readers |> List.map (fun reader -> reader.Entered) |> Task.WhenAll).WaitAsync timeout
            return ()
        }

    let private ready (session: Session) =
        task {
            do! allowInitialEvaluation session
            for _ in session.readers do let! _ = session.socket.NextBatch() in ()
            Expect.isFalse session.work.IsCompleted "Session must remain open after initial channel delivery"
            for reader in session.readers do
                Expect.equal reader.Releases 0 "Active readers must remain owned by the session"
                Expect.isFalse reader.Outputs.IsEmpty "Initial evaluation must attach the session sender"
        }

    let private finish (session: Session) =
        task {
            session.socket.End false
            let! result = session.work.WaitAsync timeout
            Expect.isSome result "Session cleanup must preserve handshake continuation"
        }

    let private released (readers: RecordingReader list) =
        for reader in readers do
            Expect.equal reader.Releases 1 "Each session-owned reader must be released exactly once"
            Expect.isTrue reader.DetachedAtRelease "Detach the session sender before calling Release"
            Expect.isTrue reader.Outputs.IsEmpty "Retired readers must not retain the session sender"

    let private batch (frame: string) =
        use json = JsonDocument.Parse frame
        let root = json.RootElement
        Expect.equal (root.GetProperty("channel").GetString()) "stream" "Channel name must be unchanged"
        Expect.isFalse (String.IsNullOrEmpty(root.GetProperty("targetId").GetString())) "Channel messages must retain their element ID"
        root.GetProperty("data").EnumerateArray()
        |> Seq.map (fun item -> JsonSerializer.Deserialize<string>(item.GetString()))
        |> Seq.toList

    let private terminal reason =
        withFixture [RecordingChannel("active", false)] (fun fixture ->
            task {
                let! session = fixture.StartSession()
                let reader = session.readers.Head
                do! reader.Entered.WaitAsync timeout
                match reason with
                | NormalClose -> session.socket.End false
                | ReceiveFailure -> session.socket.End true
                | AppCancellation -> fixture.DisposeApp() |> ignore
                do! session.socket.ReceiveEnded.WaitAsync timeout
                Expect.isFalse session.work.IsCompleted "Shutdown must wait for the gated channel evaluation/update worker"
                Expect.equal reader.Releases 0 "Do not release readers while the update worker is evaluating them"
                reader.AllowEvaluation()
                let! result = session.work.WaitAsync timeout
                Expect.isSome result "All exit paths must retain handshake forwarding"
                released session.readers
                Expect.equal session.socket.Closes (if reason = NormalClose then 1 else 0) "Only protocol close must invoke CloseAsync"
                if reason = ReceiveFailure then
                    Expect.isTrue (fixture.Errors |> Array.exists (fun error -> Object.ReferenceEquals(error.Exception, session.socket.Failure))) "Receive failure must retain its original diagnostic exception"
                if reason = AppCancellation then do! fixture.DisposeApp().WaitAsync timeout
            }
        )

    [<Tests>]
    let tests =
        testList "MutableApp Channel Tests" [
            testCaseAsync "Session exits release readers after the update worker finishes" (async {
                for reason in [NormalClose; ReceiveFailure; AppCancellation] do
                    do! terminal reason |> Async.AwaitTask
            })

            testCaseAsync "DOM removal releases only the retired reader and preserves channel messages" (async {
                do! withFixture [RecordingChannel("active", false); RecordingChannel("removed", false)] (fun fixture ->
                    task {
                        let! session = fixture.StartSession()
                        // Read the independent wire protocol oracle rather than executing generated DOM code.
                        do! allowInitialEvaluation session
                        let! first = session.socket.NextBatch()
                        let! second = session.socket.NextBatch()
                        Expect.equal ([batch first; batch second] |> List.concat |> List.sort) ["active:0"; "removed:0"] "Initial channel payloads must be preserved"
                        fixture.App.UpdateSync(Guid.Empty, [false])
                        let! removed = session.socket.NextBatch()
                        Expect.equal (batch removed) ["commit-suicide"] "Ordinary DOM removal must keep its teardown message"
                        released [session.readers.[1]]
                        Expect.equal session.readers.[0].Releases 0 "Removing a sibling must retain the live channel"
                        do! finish session
                        released session.readers
                    }) |> Async.AwaitTask
            })

            testCaseAsync "Closing one session keeps another session's reader and updates alive" (async {
                let channel = RecordingChannel("shared", false)
                do! withFixture [channel] (fun fixture ->
                    task {
                        let! first = fixture.StartSession()
                        do! ready first
                        let! second = fixture.StartSession()
                        do! ready second
                        Expect.isFalse (Object.ReferenceEquals(first.readers.Head, second.readers.Head)) "Each session must own its own reader"
                        do! finish first
                        released first.readers
                        Expect.equal second.readers.Head.Releases 0 "Another session must not be released"
                        Expect.isFalse second.readers.Head.Outputs.IsEmpty "The other session sender must remain attached"
                        channel.ChangeValue 1
                        let! update = second.socket.NextBatch()
                        Expect.equal (batch update) ["shared:1"] "The surviving session must still deliver adaptive updates"
                        Expect.isFalse second.work.IsCompleted "The other session must remain open"
                        do! finish second
                        released (first.readers @ second.readers)
                    }) |> Async.AwaitTask
            })

            testCaseAsync "Failing Release attempts cannot strand other active readers" (async {
                do! withFixture [RecordingChannel("aFail", true); RecordingChannel("bFail", true); RecordingChannel("healthy", false)] (fun fixture ->
                    task {
                        let! session = fixture.StartSession()
                        do! ready session
                        do! finish session
                        released session.readers
                    }) |> Async.AwaitTask
            })
        ]
