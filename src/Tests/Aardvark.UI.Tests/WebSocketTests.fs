namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.Extensions.DependencyInjection
open Aardvark.UI
open Expecto

module ``WebSocket Tests`` =

    type private Operation = Send | Receive | Close

    let private timeout = TimeSpan.FromSeconds 5.0
    let private payload = [| 1uy; 2uy; 3uy |]

    type private ControlledSocket(blocked: Operation option, ?disposeError: exn, ?closeFailure: (unit -> Task)) =
        inherit System.Net.WebSockets.WebSocket()

        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let calls = ConcurrentQueue<Operation>()
        let tokens = ConcurrentQueue<CancellationToken>()
        let mutable disposeCount = 0
        let mutable closeFailure = closeFailure

        let enter operation (token: CancellationToken) =
            // Record entry even for canceled tokens: canceled semaphore waiters must not get here.
            calls.Enqueue operation
            tokens.Enqueue token
            if blocked = Some operation then entered.TrySetResult(()) |> ignore
            token.ThrowIfCancellationRequested()
            if blocked = Some operation then release.Task.WaitAsync(token) :> Task
            else Task.CompletedTask

        member _.Entered = entered.Task :> Task
        member _.Calls = calls.ToArray() |> Array.toList
        member _.Tokens = tokens.ToArray() |> Array.toList
        member _.DisposeCount = Volatile.Read(&disposeCount)
        member _.Release() = release.TrySetResult(()) |> ignore

        override _.State = WebSocketState.Open
        override _.CloseStatus = Nullable<WebSocketCloseStatus>()
        override _.CloseStatusDescription = null
        override _.SubProtocol = null
        override this.Abort() = this.Release()
        override this.Dispose() =
            Interlocked.Increment(&disposeCount) |> ignore
            this.Release()
            disposeError |> Option.iter raise
        override _.CloseOutputAsync(_, _, _) = failwith "Unexpected CloseOutputAsync"

        override _.SendAsync(data: ArraySegment<byte>, messageType, endOfMessage, token) =
            Expect.sequenceEqual data payload "Changed send payload"
            Expect.equal messageType WebSocketMessageType.Binary "Changed send opcode"
            Expect.isFalse endOfMessage "Changed fragmentation flag"
            enter Send token

        override _.ReceiveAsync(data: ArraySegment<byte>, token) =
            task {
                do! enter Receive token
                Array.Copy(payload, 0, data.Array, data.Offset, payload.Length)
                return WebSocketReceiveResult(payload.Length, WebSocketMessageType.Binary, true)
            }

        override _.CloseAsync(status, description, token) =
            Expect.equal status WebSocketCloseStatus.NormalClosure "Changed close status"
            Expect.isNull description "Changed close description"
            let work = enter Close token
            let failure = closeFailure
            closeFailure <- None
            match failure with
            | Some fail when work.IsCompletedSuccessfully -> fail()
            | Some fail ->
                task {
                    do! work
                    do! fail()
                }
            | None -> work

    let private operate (socket: IWebSocket) operation token : Task =
        match operation with
        | Send -> socket.Send(WebSocketOpCode.Binary, payload, token, false)
        | Close -> socket.Close token
        | Receive ->
            task {
                let buffer = SocketBuffer(16)
                buffer.Write [| 9uy |]
                let! opcode = socket.Receive(buffer, token)
                Expect.equal opcode WebSocketOpCode.Binary "Changed receive opcode"
                Expect.sequenceEqual buffer.Data [| 9uy; 1uy; 2uy; 3uy |] "Changed buffer contents"
                Expect.equal buffer.Size 16 "Unexpected buffer growth"
            }

    let private canceled token (operation: Task) =
        task {
            try
                do! operation.WaitAsync timeout
                failtest "Expected cancellation"
            with :? OperationCanceledException as error ->
                Expect.isTrue operation.IsCanceled "Expected a canceled task, not a fault"
                Expect.equal error.CancellationToken token "Changed cancellation token"
        }

    let private withNative (native: ControlledSocket) (action: ControlledSocket -> IWebSocket -> (Task -> Task) -> CancellationToken -> Task) =
        task {
            use shutdown = new CancellationTokenSource()
            let context = DefaultHttpContext()
            context.Features.Set<IHttpWebSocketFeature>(
                { new IHttpWebSocketFeature with
                    member _.IsWebSocketRequest = true
                    member _.AcceptAsync _ = Task.FromResult(native :> System.Net.WebSockets.WebSocket) }
            )

            let handler =
                Giraffe.HttpBackend.Instance.handShake (fun socket _ ->
                    task {
                        use socket = socket
                        let pending = ResizeArray<Task>()
                        let track (operation: Task) =
                            pending.Add operation
                            operation

                        let mutable failure = None
                        try
                            try do! action native socket track shutdown.Token
                            with error -> failure <- Some (ExceptionDispatchInfo.Capture error)
                        finally
                            // Unblock holders and cancel recovery waiters even when an assertion fails.
                            native.Release()
                            shutdown.Cancel()

                        try do! Task.WhenAll(pending).WaitAsync timeout
                        with :? OperationCanceledException -> ()
                        failure |> Option.iter (fun error -> error.Throw())
                    }
                )

            try
                let! result = handler (Some >> Task.FromResult) context
                Expect.isSome result "Expected the public handshake adapter to complete"
            finally
                if native.DisposeCount = 0 then native.Dispose()
        }

    let private withSocket blocked action = withNative (new ControlledSocket(blocked)) action

    let private queued holderOperation waiterOperation =
        withSocket (Some holderOperation) (fun native socket track token ->
            task {
                let holder = operate socket holderOperation token |> track
                do! native.Entered.WaitAsync timeout
                use cancellation = new CancellationTokenSource()
                let waiter = operate socket waiterOperation cancellation.Token |> track
                Expect.isFalse waiter.IsCompleted "Expected a queued operation"

                cancellation.Cancel()
                do! canceled cancellation.Token waiter
                Expect.isFalse holder.IsCompleted "Cancellation must not finish the unrelated holder"
                Expect.equal native.Calls [holderOperation] "Canceled waiter entered the socket"

                // A canceled acquisition must not release a semaphore it never acquired.
                let recovery = operate socket holderOperation token |> track
                Expect.isFalse recovery.IsCompleted "Holder lost exclusive access"
                Expect.equal native.Calls [holderOperation] "Recovery entered before the holder released"
                native.Release()
                do! Task.WhenAll(holder, recovery).WaitAsync timeout
                Expect.equal native.Calls [holderOperation; holderOperation] "Canceled waiter entered after release"

                for operation in [Send; Receive; Close] do
                    do! (operate socket operation token |> track).WaitAsync timeout
                Expect.equal native.Calls [holderOperation; holderOperation; Send; Receive; Close] "Recovery failed"
            }
        )

    let private disposeRepeatedly (native: ControlledSocket) (socket: IWebSocket) =
        socket.Dispose()
        Expect.equal native.DisposeCount 1 "Explicit wrapper disposal must immediately dispose the native socket"
        socket.Dispose()
        Expect.equal native.DisposeCount 1 "Repeated wrapper disposal must not dispose the native socket twice"
        Parallel.For(0, 16, fun _ -> socket.Dispose()) |> ignore
        Expect.equal native.DisposeCount 1 "Concurrent wrapper disposal must remain idempotent"

    let private expectDisposed (socket: IWebSocket) =
        task {
            for operation in [Send; Receive; Close] do
                try
                    do! (operate socket operation CancellationToken.None).WaitAsync timeout
                    failtest $"Disposed wrapper must reject {operation} before entering the native socket"
                with :? ObjectDisposedException -> ()
        }

    let private awaitEntry (entered: Task) (work: Task) =
        task {
            let! _ = Task.WhenAny(entered, work).WaitAsync timeout
            if not entered.IsCompleted then
                do! work
                failtest "Handler completed before entering its gated stage"
            do! entered
        }

    type private ObservedReadStream(inner: Stream) =
        inherit Stream()
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        member _.PendingRead = entered.Task :> Task
        override _.CanRead = inner.CanRead
        override _.CanWrite = inner.CanWrite
        override _.CanSeek = inner.CanSeek
        override _.Length = inner.Length
        override _.Position with get() = inner.Position and set value = inner.Position <- value
        override _.Flush() = inner.Flush()
        override _.FlushAsync token = inner.FlushAsync token
        override _.Seek(offset, origin) = inner.Seek(offset, origin)
        override _.SetLength length = inner.SetLength length
        override _.Read(buffer, offset, count) = inner.Read(buffer, offset, count)
        override _.Write(buffer, offset, count) = inner.Write(buffer, offset, count)
        override _.ReadAsync(buffer: byte[], offset, count, token) =
            let work = inner.ReadAsync(buffer, offset, count, token)
            if not work.IsCompleted then entered.TrySetResult(()) |> ignore
            work
        override _.ReadAsync(buffer: Memory<byte>, token) =
            let work = inner.ReadAsync(buffer, token)
            if not work.IsCompleted then entered.TrySetResult(()) |> ignore
            work
        override _.WriteAsync(buffer: byte[], offset, count, token) = inner.WriteAsync(buffer, offset, count, token)
        override _.WriteAsync(buffer: ReadOnlyMemory<byte>, token) = inner.WriteAsync(buffer, token)
        override _.Dispose disposing =
            if disposing then inner.Dispose()
            base.Dispose disposing

    let private closePendingReceive useAdapter =
        task {
            use shutdown = new CancellationTokenSource(TimeSpan.FromSeconds 10.0)
            let token = shutdown.Token
            let listener = new TcpListener(IPAddress.Loopback, 0)
            listener.Start()
            try
                use client = new TcpClient()
                do! client.ConnectAsync(IPAddress.Loopback, (listener.LocalEndpoint :?> IPEndPoint).Port, token)
                use! server = listener.AcceptTcpClientAsync(token)
                use stream = new ObservedReadStream(server.GetStream())
                use native = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan)
                use peer = WebSocket.CreateFromStream(client.GetStream(), false, null, Timeout.InfiniteTimeSpan)
                let pending = ResizeArray<Task>()
                let peerWork =
                    task {
                        let! received = peer.ReceiveAsync(ArraySegment(Array.zeroCreate<byte> 16), token)
                        Expect.equal received.MessageType WebSocketMessageType.Close "The peer's first message must be Close, not application data"
                        Expect.equal received.CloseStatus (Nullable WebSocketCloseStatus.NormalClosure) "Preserve NormalClosure on the real wire"
                        Expect.isTrue (String.IsNullOrEmpty received.CloseStatusDescription) "Do not add a close reason"
                        Expect.equal received.Count 0 "No application payload accompanies closure"
                        Expect.isTrue received.EndOfMessage "Close is a complete control message"
                        // Acknowledge only the close frame; never send application data to release the server receive.
                        do! peer.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, token)
                    }
                pending.Add peerWork
                let complete (receiving: Task<WebSocketOpCode>) close =
                    task {
                        pending.Add receiving
                        do! stream.PendingRead.WaitAsync timeout
                        Expect.isFalse receiving.IsCompleted "An actual managed receive must be pending before Close begins"
                        let closing: Task = close()
                        pending.Add closing
                        let mutable failure = None
                        try
                            try do! peerWork.WaitAsync(TimeSpan.FromSeconds 2.0)
                            with :? TimeoutException -> failtest "Peer received no NormalClosure while receive was pending; closing must not require application data"
                            let! opcode = receiving.WaitAsync timeout
                            Expect.equal opcode WebSocketOpCode.Close "The outstanding receive must observe the peer's close acknowledgement"
                            do! closing.WaitAsync timeout
                            Expect.equal native.State WebSocketState.Closed "Native close handshake must finish"
                            Expect.equal peer.State WebSocketState.Closed "Peer close handshake must finish"
                        with error -> failure <- Some (ExceptionDispatchInfo.Capture error)
                        if failure.IsSome then
                            // Drain before the public adapter disposes its semaphores on callback failure.
                            shutdown.Cancel()
                            native.Abort()
                            peer.Abort()
                            try do! Task.WhenAll(receiving :> Task, closing, peerWork).WaitAsync timeout
                            with
                            | :? TimeoutException as error -> raise error
                            | _ -> ()
                        failure |> Option.iter (fun error -> error.Throw())
                    }
                let mutable failure = None
                try
                    try
                        if useAdapter then
                            let context = DefaultHttpContext()
                            context.Features.Set<IHttpWebSocketFeature>(
                                { new IHttpWebSocketFeature with
                                    member _.IsWebSocketRequest = true
                                    member _.AcceptAsync _ = Task.FromResult native })
                            let handler = Giraffe.HttpBackend.Instance.handShake (fun socket received ->
                                task {
                                    Expect.isTrue (Object.ReferenceEquals(received, context)) "Public handshake preserves its context"
                                    let buffer = SocketBuffer(16)
                                    do! complete (socket.Receive(buffer, token)) (fun () -> socket.Close token)
                                    Expect.equal buffer.Position 0 "The receive completes without application data"
                                })
                            let work = handler (Some >> Task.FromResult) context
                            pending.Add work
                            let! result = work.WaitAsync timeout
                            Expect.isSome result "Public handshake adapter completes after closure"
                        else
                            // Independent native control uses the same real streams without the adapter's locks.
                            let receiving = task {
                                let! received = native.ReceiveAsync(ArraySegment(Array.zeroCreate<byte> 16), token)
                                Expect.equal received.MessageType WebSocketMessageType.Close "Native receive observes the acknowledgement"
                                Expect.equal received.Count 0 "Native control receives no application data"
                                return WebSocketOpCode.Close
                            }
                            do! complete receiving (fun () -> native.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token))
                    with error -> failure <- Some (ExceptionDispatchInfo.Capture error)
                finally
                    shutdown.Cancel()
                    native.Abort()
                    peer.Abort()
                // Abort/cancel every native operation before draining, including when the original adapter deadlocks.
                try do! Task.WhenAll(pending).WaitAsync timeout
                with
                | :? TimeoutException as error -> raise error
                | error when failure.IsNone -> failure <- Some (ExceptionDispatchInfo.Capture error)
                | _ -> ()
                failure |> Option.iter (fun error -> error.Throw())
            finally
                listener.Stop()
        }

    type private Completion =
        | Return
        | SynchronousFailure
        | AsynchronousFailure
        | Cancellation
        | SynchronousNextFailure
        | AsynchronousNextFailure

    let private lifetime completion disposeEarly returnContext =
        task {
            let native = new ControlledSocket(None)
            use cancellation = new CancellationTokenSource()
            let context = DefaultHttpContext()
            context.RequestAborted <- cancellation.Token
            let mutable accepts = 0
            context.Features.Set<IHttpWebSocketFeature>(
                { new IHttpWebSocketFeature with
                    member _.IsWebSocketRequest = true
                    member _.AcceptAsync _ =
                        accepts <- accepts + 1
                        Task.FromResult(native :> System.Net.WebSockets.WebSocket) }
            )
            let callbackEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let callbackRelease = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let nextEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let nextRelease = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable wrapper = None
            let mutable callbackCalls = 0
            let mutable nextCalls = 0
            let expectedError : exn =
                if completion = Cancellation then OperationCanceledException(cancellation.Token)
                else InvalidOperationException($"{completion}")
            let expectedResult = if returnContext then Some (DefaultHttpContext() :> HttpContext) else None
            let callbackFailed = completion = SynchronousFailure || completion = AsynchronousFailure || completion = Cancellation
            let pendingDisposeCount = if disposeEarly then 1 else 0
            let checkContext (received: HttpContext) =
                Expect.isTrue (Object.ReferenceEquals(received, context)) "Handshake must forward the original HTTP context"
                Expect.equal received.RequestAborted cancellation.Token "Handshake must preserve the request cancellation token"

            let continuation socket received : Task =
                wrapper <- Some socket
                callbackCalls <- callbackCalls + 1
                checkContext received
                Expect.equal native.DisposeCount 0 "Accepted native socket must be alive on continuation entry"
                if completion = SynchronousFailure then
                    if disposeEarly then disposeRepeatedly native socket
                    raise expectedError
                else
                    task {
                        do! operate socket Send received.RequestAborted
                        if disposeEarly then disposeRepeatedly native socket
                        callbackEntered.TrySetResult(()) |> ignore
                        do! callbackRelease.Task
                        if completion = AsynchronousFailure || completion = Cancellation then
                            return raise expectedError
                    }

            let next received =
                nextCalls <- nextCalls + 1
                checkContext received
                Expect.equal native.DisposeCount 1 "Native socket must be disposed exactly once before downstream entry"
                nextEntered.TrySetResult(()) |> ignore
                if completion = SynchronousNextFailure then
                    raise expectedError
                else
                    task {
                        do! nextRelease.Task
                        if completion = AsynchronousNextFailure then raise expectedError
                        return expectedResult
                    }

            let handler = Giraffe.HttpBackend.Instance.handShake continuation
            let work = handler next context
            let mutable failure = None
            try
                if completion <> SynchronousFailure then
                    do! awaitEntry callbackEntered.Task work
                    Expect.isFalse work.IsCompleted "Handler must wait for its continuation"
                    Expect.equal native.DisposeCount pendingDisposeCount "Pending continuation must preserve explicit disposal or keep the native socket alive"
                    Expect.equal nextCalls 0 "Downstream handler must not run before the continuation completes"
                    if completion = Cancellation then cancellation.Cancel()
                    callbackRelease.TrySetResult(()) |> ignore

                    if not callbackFailed then
                        do! awaitEntry nextEntered.Task work
                        if completion <> SynchronousNextFailure then
                            Expect.isFalse work.IsCompleted "Handler must wait for the downstream task"
                            Expect.equal native.DisposeCount 1 "Pending downstream work must not keep the native socket alive"
                            do! expectDisposed (Option.get wrapper)
                            nextRelease.TrySetResult(()) |> ignore

                if completion = Return then
                    let! result = work.WaitAsync timeout
                    match result, expectedResult with
                    | Some actual, Some expected ->
                        Expect.isTrue (Object.ReferenceEquals(actual, expected)) "Downstream result must be forwarded unchanged"
                    | None, None -> ()
                    | _ -> failtest "Changed downstream result"
                else
                    let! error =
                        task {
                            try
                                let! _ = work.WaitAsync timeout
                                return failtest "Expected handler failure"
                            with error -> return error
                        }
                    Expect.isTrue (Object.ReferenceEquals(error, expectedError)) "Cleanup must preserve the exact failure object"
                    if completion = Cancellation then
                        Expect.isTrue work.IsCanceled "Cancellation must remain a canceled task"
                        Expect.equal (error :?> OperationCanceledException).CancellationToken cancellation.Token "Cleanup must preserve the cancellation token"
                    else
                        Expect.isTrue work.IsFaulted "Failure must remain a faulted task"

                Expect.equal accepts 1 "Handshake must accept exactly one native socket"
                Expect.equal callbackCalls 1 "Continuation must run exactly once"
                Expect.equal nextCalls (if callbackFailed then 0 else 1) "Changed downstream-handler invocation behavior"
                Expect.equal native.DisposeCount 1 "Handler must dispose its accepted socket exactly once"
                Expect.equal native.Tokens (if completion = SynchronousFailure then [] else [cancellation.Token]) "Wrapper must forward the original operation token"
                let socket = Option.get wrapper
                do! expectDisposed socket
                disposeRepeatedly native socket
                Expect.equal native.DisposeCount 1 "Handshake cleanup and later wrapper disposal must still dispose the native socket only once"
            with error ->
                failure <- Some (ExceptionDispatchInfo.Capture error)

            // Release every gate and drain the handler before disposing test-owned fallbacks.
            callbackRelease.TrySetResult(()) |> ignore
            nextRelease.TrySetResult(()) |> ignore
            cancellation.Cancel()
            try
                try
                    let! _ = work.WaitAsync timeout
                    ()
                with
                | :? TimeoutException as error ->
                    if failure.IsNone then failure <- Some (ExceptionDispatchInfo.Capture error)
                | _ -> () // The expected fault/cancellation has already been checked above.
            finally
                wrapper |> Option.iter (fun socket -> socket.Dispose())
                if native.DisposeCount = 0 then native.Dispose()
            failure |> Option.iter (fun error -> error.Throw())
        }

    [<Tests>]
    let tests =
        testList "WebSocket Tests.Giraffe" [
            testCaseAsync "Accepted sockets are disposed before downstream with explicit disposal or fallback" (async {
                for disposeEarly in [false; true] do
                    for returnContext in [false; true] do
                        do! lifetime Return disposeEarly returnContext |> Async.AwaitTask
            })

            testCaseAsync "Explicit disposal and fallback preserve callback failures, cancellation and downstream failures" (async {
                for completion in [SynchronousFailure; AsynchronousFailure; Cancellation; SynchronousNextFailure; AsynchronousNextFailure] do
                    for disposeEarly in [false; true] do
                        do! lifetime completion disposeEarly true |> Async.AwaitTask
            })

            testCaseAsync "Disposal failures release semaphores once and prevent downstream entry" (async {
                for disposeEarly in [false; true] do
                    let disposalError = InvalidOperationException("native disposal failed")
                    let callbackError = InvalidOperationException("callback failed after cleanup")
                    let native = new ControlledSocket(None, disposeError = disposalError)
                    let context = DefaultHttpContext()
                    let mutable wrapper = None
                    let mutable nextCalls = 0
                    context.Features.Set<IHttpWebSocketFeature>(
                        { new IHttpWebSocketFeature with
                            member _.IsWebSocketRequest = true
                            member _.AcceptAsync _ = Task.FromResult(native :> System.Net.WebSockets.WebSocket) }
                    )
                    let handler =
                        Giraffe.HttpBackend.Instance.handShake (fun socket _ ->
                            wrapper <- Some socket
                            if disposeEarly then
                                task {
                                    try
                                        socket.Dispose()
                                        failtest "Expected native disposal failure"
                                    with error ->
                                        Expect.isTrue (Object.ReferenceEquals(error, disposalError)) "Wrapper must preserve native disposal failure identity"
                                    Expect.equal native.DisposeCount 1 "Native disposal must be attempted once before test cleanup"
                                    do! expectDisposed socket
                                    Expect.isEmpty native.Calls "Both semaphores must be released even when native disposal throws"
                                    disposeRepeatedly native socket
                                    return raise callbackError
                                }
                            else
                                Task.CompletedTask
                        )
                    let next _ =
                        nextCalls <- nextCalls + 1
                        Task.FromResult(Some (context :> HttpContext))
                    try
                        let work = handler next context
                        let! error =
                            task {
                                try
                                    let! _ = work.WaitAsync timeout
                                    return failtest "Expected callback or fallback-disposal failure"
                                with error -> return error
                            } |> Async.AwaitTask
                        let expectedError = if disposeEarly then callbackError else disposalError
                        Expect.isTrue (Object.ReferenceEquals(error, expectedError)) "Scoped cleanup must propagate the exact failure without retrying disposal"
                        Expect.isTrue work.IsFaulted "Disposal failure must remain a faulted task"
                        Expect.equal nextCalls 0 "Failed callback or fallback disposal must prevent downstream entry"
                        Expect.equal native.DisposeCount 1 "Failure cleanup must dispose the native socket exactly once before test cleanup"
                        let socket = Option.get wrapper
                        do! expectDisposed socket |> Async.AwaitTask
                        Expect.isEmpty native.Calls "Failure cleanup must dispose both semaphores"
                        disposeRepeatedly native socket
                    finally
                        wrapper |> Option.iter (fun socket -> socket.Dispose())
                        if native.DisposeCount = 0 then
                            try native.Dispose() with error when Object.ReferenceEquals(error, disposalError) -> ()
            })

            testCaseAsync "Non-WebSocket requests remain HTTP 400 without acceptance or callbacks" (async {
                use native = new ControlledSocket(None)
                use body = new MemoryStream()
                use services =
                    new ServiceCollection()
                    |> global.Giraffe.Middleware.ServiceCollectionExtensions.AddGiraffe
                    |> fun services -> services.BuildServiceProvider()
                let context = DefaultHttpContext()
                context.RequestServices <- services
                context.Request.Headers.Accept <- "text/plain"
                context.Response.Body <- body
                let mutable accepts = 0
                context.Features.Set<IHttpWebSocketFeature>(
                    { new IHttpWebSocketFeature with
                        member _.IsWebSocketRequest = false
                        member _.AcceptAsync _ =
                            accepts <- accepts + 1
                            Task.FromResult(native :> System.Net.WebSockets.WebSocket) }
                )
                let handler =
                    Giraffe.HttpBackend.Instance.handShake (fun _ _ ->
                        failtest "HTTP 400 must not invoke a WebSocket continuation")
                let! result = handler (fun _ -> failtest "HTTP 400 must not invoke the downstream handler") context |> Async.AwaitTask
                Expect.equal context.Response.StatusCode 400 "Changed non-WebSocket status"
                Expect.isTrue (result |> Option.exists (fun returned -> Object.ReferenceEquals(returned, context))) "HTTP 400 must return its original context"
                Expect.equal (System.Text.Encoding.UTF8.GetString(body.ToArray())) "Expected web socket request" "Changed non-WebSocket response body"
                Expect.equal accepts 0 "HTTP 400 must not accept a socket"
                Expect.equal native.DisposeCount 0 "Unaccepted sockets are not owned by handShake"
            })

            for holder, waiter in [Send, Send; Receive, Receive; Send, Close; Close, Send; Close, Close] do
                testCaseAsync $"Cancel {waiter} queued behind {holder}" (async {
                    do! queued holder waiter |> Async.AwaitTask
                })

            testCaseAsync "Close may run with a receive pending without releasing receive exclusivity" (async {
                do! withSocket (Some Receive) (fun native socket track token ->
                    task {
                        let holder = operate socket Receive token |> track
                        do! native.Entered.WaitAsync timeout
                        do! (operate socket Close token |> track).WaitAsync timeout
                        Expect.isFalse holder.IsCompleted "Close must not finish the unrelated receive"
                        Expect.equal native.Calls [Receive; Close] "Close must reach the native socket before receive finishes"
                        let recovery = operate socket Receive token |> track
                        Expect.isFalse recovery.IsCompleted "A second receive must stay exclusive"
                        do! (operate socket Send token |> track).WaitAsync timeout
                        Expect.equal native.Calls [Receive; Close; Send] "Close released send, not receive"
                        native.Release()
                        do! Task.WhenAll(holder, recovery).WaitAsync timeout
                        Expect.equal native.Calls [Receive; Close; Send; Receive] "Receive ownership remains intact after close"
                    }) |> Async.AwaitTask
            })

            testCaseAsync "Native close cancellation releases send for queued sends and closes" (async {
                do! withSocket (Some Close) (fun native socket track token ->
                    task {
                        use cancellation = new CancellationTokenSource()
                        let close = operate socket Close cancellation.Token |> track
                        do! native.Entered.WaitAsync timeout
                        let send = operate socket Send token |> track
                        let recovery = operate socket Close token |> track
                        Expect.isFalse send.IsCompleted "Send must wait for native close"
                        Expect.isFalse recovery.IsCompleted "Close operations must remain serialized"
                        Expect.equal native.Calls [Close] "Queued operations must not enter the native socket"
                        cancellation.Cancel()
                        do! canceled cancellation.Token close
                        do! send.WaitAsync timeout
                        Expect.isFalse recovery.IsCompleted "The later native close still waits for its gate"
                        native.Release()
                        do! recovery.WaitAsync timeout
                        for operation in [Send; Receive; Close] do
                            do! (operate socket operation token |> track).WaitAsync timeout
                        Expect.equal native.Calls [Close; Send; Close; Send; Receive; Close] "Native cancellation must release send ownership once"
                        Expect.equal native.Tokens [cancellation.Token; token; token; token; token; token] "Preserve native operation tokens"
                    }) |> Async.AwaitTask
            })

            testCaseAsync "Native close errors preserve identity and leave both semaphores usable" (async {
                for synchronous in [false; true] do
                    let error = InvalidOperationException("Injected native close failure")
                    let fail () : Task =
                        if synchronous then raise error
                        else
                            task {
                                do! Task.Yield()
                                return raise error
                            }
                    do! withNative (new ControlledSocket(None, closeFailure = fail)) (fun native socket track token ->
                        task {
                            // Observe this expected fault directly before tracking recovery operations.
                            let close = operate socket Close token
                            let! actual = task {
                                try
                                    do! close.WaitAsync timeout
                                    return failtest "Expected native close failure"
                                with actual -> return actual
                            }
                            Expect.isTrue close.IsFaulted "Native errors remain faults, not cancellation"
                            Expect.isTrue (Object.ReferenceEquals(actual, error)) "Preserve the exact native error"
                            for operation in [Send; Close; Receive] do
                                do! (operate socket operation token |> track).WaitAsync timeout
                            Expect.equal native.Calls [Close; Send; Close; Receive] "Close failure must not retain either semaphore"
                            Expect.equal native.Tokens [token; token; token; token] "Preserve native operation tokens after failure"
                        }) |> Async.AwaitTask
            })

            testCaseAsync "Real close handshake completes with a receive pending and no application data" (async {
                do! closePendingReceive false |> Async.AwaitTask
                do! closePendingReceive true |> Async.AwaitTask
            })

            testCaseAsync "Pre-canceled operations never enter the socket and leave locks usable" (async {
                do! withSocket None (fun native socket track token ->
                    task {
                        use cancellation = new CancellationTokenSource()
                        cancellation.Cancel()
                        for operation in [Send; Receive; Close] do
                            let work = operate socket operation cancellation.Token |> track
                            do! canceled cancellation.Token work
                        Expect.isEmpty native.Calls "Pre-canceled operations entered the socket"

                        for operation in [Send; Receive; Close] do
                            do! (operate socket operation token |> track).WaitAsync timeout
                        Expect.equal native.Calls [Send; Receive; Close] "Locks were not usable after cancellation"
                    }) |> Async.AwaitTask
            })

            testCaseAsync "Ping and Pong remain no-ops while send is held even with canceled tokens" (async {
                do! withSocket (Some Send) (fun native socket track token ->
                    task {
                        let holder = operate socket Send token |> track
                        do! native.Entered.WaitAsync timeout
                        use cancellation = new CancellationTokenSource()
                        cancellation.Cancel()
                        for pingToken in [token; cancellation.Token] do
                            for opcode in [WebSocketOpCode.Ping; WebSocketOpCode.Pong] do
                                do! (socket.Send(opcode, Array.empty, pingToken) |> track).WaitAsync timeout
                        Expect.isFalse holder.IsCompleted "Ping/Pong finished the unrelated send"
                        Expect.equal native.Calls [Send] "Ping/Pong reached the underlying socket"
                        native.Release()
                        do! holder.WaitAsync timeout
                    }) |> Async.AwaitTask
            })
        ]
