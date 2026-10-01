namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
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

        let enter operation (token: CancellationToken) =
            // Record entry even for canceled tokens: canceled semaphore waiters must not get here.
            calls.Enqueue operation
            if blocked = Some operation then entered.TrySetResult(()) |> ignore
            token.ThrowIfCancellationRequested()
            if blocked = Some operation then release.Task.WaitAsync(token) :> Task
            else Task.CompletedTask

        member _.Entered = entered.Task :> Task
        member _.Calls = calls.ToArray() |> Array.toList
        member _.Release() = release.TrySetResult(()) |> ignore

        override _.State = WebSocketState.Open
        override _.CloseStatus = Nullable<WebSocketCloseStatus>()
        override _.CloseStatusDescription = null
        override _.SubProtocol = null
        override this.Abort() = this.Release()
        override this.Dispose() = this.Release()
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
            use native = new ControlledSocket(blocked)
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

            let! result = handler (Some >> Task.FromResult) context
            Expect.isSome result "Expected the public handshake adapter to complete"
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

    [<Tests>]
    let tests =
        testList "WebSocket Tests.Giraffe" [
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
