namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.IO
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

    type private ControlledSocket(blocked: Operation option) =
        inherit System.Net.WebSockets.WebSocket()

        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let calls = ConcurrentQueue<Operation>()
        let tokens = ConcurrentQueue<CancellationToken>()
        let mutable disposeCount = 0

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
            enter Close token

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

    let private withSocket blocked (action: ControlledSocket -> IWebSocket -> (Task -> Task) -> CancellationToken -> Task) =
        task {
            let native = new ControlledSocket(blocked)
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

    type private Completion =
        | Return
        | DisposeWrapper
        | SynchronousFailure
        | AsynchronousFailure
        | Cancellation
        | SynchronousNextFailure
        | AsynchronousNextFailure

    let private lifetime completion returnContext =
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
            let succeeded = completion = Return || completion = DisposeWrapper
            let checkContext (received: HttpContext) =
                Expect.isTrue (Object.ReferenceEquals(received, context)) "Handshake must forward the original HTTP context"
                Expect.equal received.RequestAborted cancellation.Token "Handshake must preserve the request cancellation token"
                Expect.equal native.DisposeCount 0 "Native socket must remain alive inside the handler"

            let continuation socket received : Task =
                wrapper <- Some socket
                callbackCalls <- callbackCalls + 1
                checkContext received
                if completion = SynchronousFailure then
                    raise expectedError
                else
                    task {
                        do! operate socket Send received.RequestAborted
                        if completion = DisposeWrapper then
                            socket.Dispose()
                            Expect.equal native.DisposeCount 0 "Wrapper disposal must not own the accepted native socket"
                        callbackEntered.TrySetResult(()) |> ignore
                        do! callbackRelease.Task
                        if completion = AsynchronousFailure || completion = Cancellation then
                            return raise expectedError
                    }

            let next received =
                nextCalls <- nextCalls + 1
                checkContext received
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
                    do! callbackEntered.Task.WaitAsync timeout
                    Expect.isFalse work.IsCompleted "Handler must wait for its continuation"
                    Expect.equal native.DisposeCount 0 "Pending continuation must keep the native socket alive"
                    Expect.equal nextCalls 0 "Downstream handler must not run before the continuation completes"
                    if completion = Cancellation then cancellation.Cancel()
                    callbackRelease.TrySetResult(()) |> ignore

                    if not callbackFailed then
                        do! nextEntered.Task.WaitAsync timeout
                        if completion <> SynchronousNextFailure then
                            Expect.isFalse work.IsCompleted "Handler must wait for the downstream task"
                            Expect.equal native.DisposeCount 0 "Pending downstream handler must keep the native socket alive"
                            nextRelease.TrySetResult(()) |> ignore

                if succeeded then
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
                try
                    do! (operate socket Send CancellationToken.None).WaitAsync timeout
                    failtest "Handler must also dispose its adapter wrapper"
                with :? ObjectDisposedException -> ()
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
            testCaseAsync "Accepted socket lifetime covers continuation and downstream completion" (async {
                for completion in [Return; DisposeWrapper] do
                    for returnContext in [false; true] do
                        do! lifetime completion returnContext |> Async.AwaitTask
            })

            testCaseAsync "Accepted sockets are disposed on callback failures, cancellation and downstream failures" (async {
                for completion in [SynchronousFailure; AsynchronousFailure; Cancellation; SynchronousNextFailure; AsynchronousNextFailure] do
                    do! lifetime completion true |> Async.AwaitTask
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

            for holder, waiter in [Send, Send; Receive, Receive; Send, Close] do
                testCaseAsync $"Cancel {waiter} queued behind {holder}" (async {
                    do! queued holder waiter |> Async.AwaitTask
                })

            testCaseAsync "Cancel Close waiting for receive releases send but not receive" (async {
                do! withSocket (Some Receive) (fun native socket track token ->
                    task {
                        let holder = operate socket Receive token |> track
                        do! native.Entered.WaitAsync timeout
                        use cancellation = new CancellationTokenSource()
                        let close = operate socket Close cancellation.Token |> track
                        let send = operate socket Send token |> track
                        Expect.isFalse close.IsCompleted "Expected Close to wait for receive"
                        Expect.isFalse send.IsCompleted "Close must hold send while waiting for receive"
                        Expect.equal native.Calls [Receive] "An operation bypassed Close's locks"

                        cancellation.Cancel()
                        do! canceled cancellation.Token close
                        do! send.WaitAsync timeout
                        Expect.isFalse holder.IsCompleted "Send must recover before receive finishes"
                        Expect.equal native.Calls [Receive; Send] "Canceled Close entered the socket"

                        let recovery = operate socket Receive token |> track
                        Expect.isFalse recovery.IsCompleted "Canceled Close released an unowned receive lock"
                        Expect.equal native.Calls [Receive; Send] "Receive entered before its holder released"
                        native.Release()
                        do! Task.WhenAll(holder, recovery).WaitAsync timeout
                        Expect.equal native.Calls [Receive; Send; Receive] "Canceled Close entered after release"
                        do! (operate socket Close token |> track).WaitAsync timeout
                        Expect.equal native.Calls [Receive; Send; Receive; Close] "Close failed after cancellation"
                    }) |> Async.AwaitTask
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
