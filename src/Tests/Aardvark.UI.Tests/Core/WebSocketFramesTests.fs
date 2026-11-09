namespace Aardvark.UI.Tests

open System
open System.Net
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Reflection
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Aardvark.Rendering
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``WebSocket Frames Tests`` =

    type Backend =
        | Suave
        | Giraffe

        member this.Name =
            match this with
            | Suave -> "Suave"
            | Giraffe -> "Giraffe"

    let private createServer backend (serve: IWebSocket -> CancellationToken -> Task<unit>) : TestServer =
        match backend with
        | Suave ->
            let http = Suave.HttpBackend.Instance
            SuaveTestServer(fun token -> [
                http.handShake (fun socket _ -> serve socket token :> Task)
            ])
        | Giraffe ->
            let http = Giraffe.HttpBackend.Instance
            GiraffeTestServer(fun token -> [
                http.handShake (fun socket _ -> serve socket token :> Task)
            ])

    let private send (stream: System.Net.Sockets.NetworkStream) token opcode final (data: byte[]) =
        Expect.isLessThan data.Length 65536 "Fixture frame length"
        let mask = [| 0x19uy; 0xA7uy; 0x03uy; 0xF1uy |]
        let frame = [|
            yield opcode ||| (if final then 0x80uy else 0uy)
            if data.Length < 126 then yield 0x80uy ||| byte data.Length
            else
                yield 0xFEuy
                yield byte (data.Length >>> 8)
                yield byte data.Length
            yield! mask
            for i in 0 .. data.Length - 1 do yield data.[i] ^^^ mask.[i % 4]
        |]
        stream.WriteAsync(frame.AsMemory(), token).AsTask()

    let private readBytes (stream: System.Net.Sockets.NetworkStream) token count =
        task {
            let data = Array.zeroCreate<byte> count
            do! stream.ReadExactlyAsync(data.AsMemory(), token)
            return data
        }

    let private read (stream: System.Net.Sockets.NetworkStream) token =
        task {
            let! header = readBytes stream token 2
            Expect.equal (header.[0] &&& 0x80uy) 0x80uy "Server response is final"
            Expect.equal (header.[1] &&& 0x80uy) 0uy "Server response is unmasked"
            let mutable length = int (header.[1] &&& 0x7Fuy)
            Expect.isLessThan length 127 "Fixture uses short or 16-bit frame lengths"
            if length = 126 then
                let! extended = readBytes stream token 2
                length <- int extended.[0] * 256 + int extended.[1]
            let! data = readBytes stream token length
            return header.[0] &&& 0x0Fuy, data
        }

    let private connect (stream: System.Net.Sockets.NetworkStream) token host path =
        task {
            let request = Encoding.ASCII.GetBytes $"GET {path} HTTP/1.1\r\nHost: {host}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"
            do! stream.WriteAsync(request.AsMemory(), token)
            let response = StringBuilder()
            while not (response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) do
                let! data = readBytes stream token 1
                response.Append(char data.[0]) |> ignore
                Expect.isLessThan response.Length 8192 "Bounded upgrade response"
            Expect.stringStarts (response.ToString()) "HTTP/1.1 101" "WebSocket upgrade"
            Expect.stringContains (response.ToString()) "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=" "WebSocket accept key"
        }

    let private closePayload = [| 0x03uy; 0xE8uy; byte 'b'; byte 'y'; byte 'e' |]

    let private checkFrame stream token opcode payload =
        task {
            let! actualOpcode, actualPayload = read stream token
            Expect.equal actualOpcode opcode "Unexpected wire opcode"
            Expect.sequenceEqual actualPayload payload "Unexpected wire payload"
        }

    let private delivered (messages: TaskCompletionSource<WebSocketOpCode * byte[] * int>[]) index (token: CancellationToken) opcode prefix payload =
        task {
            let! actualOpcode, actualPayload, capacity = messages.[index].Task.WaitAsync(token)
            Expect.equal actualOpcode opcode "Unexpected Receive opcode"
            Expect.sequenceEqual actualPayload (Array.append prefix payload) "Unexpected Receive bytes"
            return capacity
        }

    let private run (backend: Backend) (prefix: byte[]) (check: System.Net.Sockets.NetworkStream -> CancellationToken -> TaskCompletionSource<WebSocketOpCode * byte[] * int>[] -> Task<unit>) =
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let drained = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let messages = Array.init 3 (fun _ -> TaskCompletionSource<WebSocketOpCode * byte[] * int>(TaskCreationOptions.RunContinuationsAsynchronously))
        let serve (socket: IWebSocket) (token: CancellationToken) =
            task {
                entered.SetResult()
                try
                    let buffer = SocketBuffer(8)
                    let mutable index = 0
                    let mutable running = true
                    while running do
                        buffer.Position <- 0
                        buffer.Write prefix
                        let! opcode = socket.Receive(buffer, token)
                        let data = buffer.Data.ToArray()
                        messages.[index].SetResult(opcode, data, buffer.Size)
                        index <- index + 1
                        let response = data.[prefix.Length..]
                        match backend, opcode with
                        | Giraffe, WebSocketOpCode.Close -> do! socket.Close(token)
                        | _ ->
                            let reply = if opcode = WebSocketOpCode.Ping then WebSocketOpCode.Pong else opcode
                            do! socket.Send(reply, response, token)
                        running <- opcode <> WebSocketOpCode.Close
                finally
                    drained.TrySetResult() |> ignore
            }
        let server = createServer backend serve
        use _ = server.Acquire()
        use timeout = CancellationTokenSource.CreateLinkedTokenSource(server.CancellationToken)
        timeout.CancelAfter(TimeSpan.FromSeconds 15.0)
        let token = timeout.Token
        use client = new System.Net.Sockets.TcpClient()
        try
            let port = (Uri $"http://{server.Host}").Port
            client.ConnectAsync(IPAddress.Loopback, port, token).AsTask().GetAwaiter().GetResult()
            use stream = client.GetStream()
            (connect stream token server.Host "/frames").GetAwaiter().GetResult()
            (check stream token messages).GetAwaiter().GetResult()
            drained.Task.WaitAsync(token).GetAwaiter().GetResult()
        finally
            // Also unblock and drain the server Receive if a primary assertion fails.
            client.Dispose()
            if entered.Task.IsCompleted then
                drained.Task.WaitAsync(TimeSpan.FromSeconds 5.0).GetAwaiter().GetResult()

    let private payload opcode =
        if opcode = 1uy then Encoding.UTF8.GetBytes(String.replicate 32 "hπ🙂\000!")
        else Array.init 512 byte

    let private dataOpcode opcode = if opcode = 1uy then WebSocketOpCode.Text else WebSocketOpCode.Binary

    let private finish backend stream token messages index prefix =
        task {
            do! send stream token 8uy true closePayload
            let! responseOpcode, responsePayload = read stream token
            Expect.equal responseOpcode 8uy "Close handshake response"
            let expectedPayload =
                match backend with
                | Suave ->
                    Expect.sequenceEqual responsePayload closePayload "Suave preserves the raw Close payload"
                    closePayload
                | Giraffe -> Array.empty
            let! _ = delivered messages index token WebSocketOpCode.Close prefix expectedPayload
            return ()
        }

    let private subsequent backend stream token messages opcode prefix capacity =
        task {
            let opcode = if opcode = 1uy then 2uy else 1uy
            let next = Encoding.UTF8.GetBytes "next\000"
            do! send stream token opcode true next
            do! checkFrame stream token opcode next
            let! nextCapacity = delivered messages 1 token (dataOpcode opcode) prefix next
            Expect.equal nextCapacity capacity "Shorter message reuses buffer capacity"
            do! finish backend stream token messages 2 prefix
        }

    let fragmented backend opcode control (controlPayload: byte[]) prefix =
        run backend prefix (fun stream token messages -> task {
            let data = payload opcode
            do! send stream token opcode false data.[..1]
            do! send stream token control true controlPayload
            // A Ping after Pong is a read-order barrier: both controls must be consumed
            // before we assert that no partial data message has been dispatched.
            let pong = if control = 10uy then [| 0xB3uy; 0uy; 0xFFuy |] else controlPayload
            if control = 10uy then do! send stream token 9uy true pong
            let! responseOpcode, responsePayload = read stream token
            Expect.isFalse messages.[0].Task.IsCompleted "Control must not finish fragmented data"
            Expect.equal responseOpcode 10uy "Interleaved Ping receives Pong"
            Expect.sequenceEqual responsePayload pong "Pong matches Ping payload"
            do! send stream token 0uy false data.[2..data.Length - 2]
            do! send stream token 9uy true Array.empty
            do! checkFrame stream token 10uy Array.empty
            Expect.isFalse messages.[0].Task.IsCompleted "Non-final continuation remains pending"
            do! send stream token 0uy true data.[data.Length - 1..]
            do! checkFrame stream token opcode data
            let! capacity = delivered messages 0 token (dataOpcode opcode) prefix data
            match backend with
            | Suave -> Expect.equal capacity (2 * (prefix.Length + data.Length - 1)) "Only data grows the Suave buffer"
            | Giraffe -> Expect.isGreaterThan capacity (prefix.Length + data.Length - 1) "Giraffe buffer contains the complete message"
            do! subsequent backend stream token messages opcode prefix capacity
        })

    let closeDuringFragments backend opcode prefix =
        run backend prefix (fun stream token messages -> task {
            do! send stream token opcode false (payload opcode).[..1]
            do! send stream token 0uy false (payload opcode).[2..10]
            do! send stream token 8uy true closePayload
            let expectedPayload = if backend = Suave then closePayload else Array.empty
            let! _ = delivered messages 0 token WebSocketOpCode.Close prefix expectedPayload
            let! responseOpcode, responsePayload = read stream token
            Expect.equal responseOpcode 8uy "Close handshake response"
            if backend = Suave then
                Expect.sequenceEqual responsePayload closePayload "Suave preserves the raw Close payload"
            Expect.isFalse messages.[1].Task.IsCompleted "Incomplete data is never dispatched"
        })

    let ordinary backend opcode fragmented prefix =
        run backend prefix (fun stream token messages -> task {
            let data = payload opcode
            if fragmented then
                do! send stream token opcode false data.[..1]
                do! send stream token 0uy false data.[2..]
                do! send stream token 0uy true Array.empty
            else do! send stream token opcode true data
            do! checkFrame stream token opcode data
            let! capacity = delivered messages 0 token (dataOpcode opcode) prefix data
            match backend with
            | Suave -> Expect.equal capacity (prefix.Length + data.Length) "Ordinary Suave append and growth"
            | Giraffe -> Expect.isGreaterThan capacity (prefix.Length + data.Length - 1) "Giraffe buffer contains the ordinary message"
            do! subsequent backend stream token messages opcode prefix capacity
        })

    let standalone backend prefix =
        match backend with
        | Suave ->
            let controls = [|
                9uy, WebSocketOpCode.Ping, [| 0uy; 0xFFuy; 0x19uy |]
                10uy, WebSocketOpCode.Pong, Array.empty
            |]
            run backend prefix (fun stream token messages -> task {
                for index in 0 .. controls.Length - 1 do
                    let wireOpcode, opcode, data = controls.[index]
                    do! send stream token wireOpcode true data
                    let! _ = delivered messages index token opcode prefix data
                    do! checkFrame stream token 10uy data
                do! finish backend stream token messages 2 prefix
            })
        | Giraffe ->
            run backend prefix (fun stream token messages -> task {
                let ping = [| 0uy; 0xFFuy; 0x19uy |]
                do! send stream token 9uy true ping
                do! checkFrame stream token 10uy ping
                do! send stream token 10uy true [| 0x42uy; 0uy |]
                let data = Encoding.UTF8.GetBytes "after-controls"
                do! send stream token 1uy true data
                do! checkFrame stream token 1uy data
                let! _ = delivered messages 0 token WebSocketOpCode.Text prefix data
                do! finish backend stream token messages 1 prefix
            })

    // Public because DispatchProxy generates a derived type in another assembly.
    type RejectingRuntime() =
        inherit DispatchProxy()
        let mutable calls = 0
        member _.Calls = Volatile.Read(&calls)
        override _.Invoke(methodInfo, _) =
            Interlocked.Increment(&calls) |> ignore
            invalidOp $"Unexpected rendering runtime operation: {methodInfo.Name}"

    let private rejectingRuntime () =
        let runtime = DispatchProxy.Create<IRuntime, RejectingRuntime>()
        runtime, (box runtime :?> RejectingRuntime)

    let private pings = [|
        Array.empty
        [| 0uy; 0xFFuy; 0x80uy |]
        Array.init 125 (fun i -> byte ((37 * i + 128) % 256))
        [| 0xA3uy |]
        [| 0xFEuy; 0uy |]
        Array.empty
    |]

    let private eventPings (payloads: byte[][]) =
        let runtime, guard = rejectingRuntime()
        use app =
            { initial = 0
              unpersist = { create = AVal.init; update = fun model value -> model.Value <- value }
              update = fun _ value -> value
              threads = fun _ -> ThreadPool.empty
              view = fun _ -> div [onEvent "onwire" [] (List.exactlyOne >> Int32.Parse)] [] }
            |> App.start
        let handled = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        use _messages = app.Messages.Subscribe(fun value -> handled.TrySetResult value |> ignore)
        let errors = ConcurrentQueue<exn>()
        use _internal = app.InternalError.Subscribe(fun error -> errors.Enqueue error.Exception)
        use _application = app.ApplicationError.Subscribe(fun error -> errors.Enqueue error.Exception)
        let handler = MutableApp.toWebPart Suave.HttpBackend.Instance runtime app
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let drained = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let server = SuaveTestServer(fun _ -> [fun context -> async {
            entered.TrySetResult() |> ignore
            try return! handler context
            finally drained.TrySetResult() |> ignore
        }])
        use _server = server.Acquire()
        use timeout = CancellationTokenSource.CreateLinkedTokenSource(server.CancellationToken)
        timeout.CancelAfter(TimeSpan.FromSeconds 15.0)
        let token = timeout.Token
        use client = new System.Net.Sockets.TcpClient()
        try
            client.ConnectAsync(IPAddress.Loopback, (Uri $"http://{server.Host}").Port, token).AsTask().GetAwaiter().GetResult()
            use stream = client.GetStream()
            let session = Guid.NewGuid()
            let check = task {
                do! connect stream token server.Host $"/events?session={session}"
                let! opcode, script = read stream token
                Expect.equal opcode 1uy "Real /events endpoint sends its initial DOM update"
                let activation = Regex.Match(Encoding.UTF8.GetString script, "aardvark\\.setEventHandler\\(\"([^\"]+)\", \"onwire\", (\\d+)\\);")
                Expect.isTrue activation.Success "Find the registered handler ID/version in the emitted protocol update"
                let replies = ResizeArray<byte[]>()
                for payload in payloads do
                    do! send stream token 9uy true payload
                    let! replyOpcode, reply = read stream token
                    Expect.equal replyOpcode 10uy "Standalone Ping receives Pong"
                    replies.Add reply
                for heartbeat in ["#"; "#ping with text and 東京"] do
                    do! send stream token 1uy true (Encoding.UTF8.GetBytes heartbeat)
                    do! checkFrame stream token 10uy Array.empty
                let message = JsonSerializer.Serialize {| sender = activation.Groups.[1].Value; name = "onwire"; version = Int32.Parse activation.Groups.[2].Value; args = [| "42" |] |}
                do! send stream token 1uy true (Encoding.UTF8.GetBytes message)
                let! value = handled.Task.WaitAsync token
                Expect.equal value 42 "Ordinary event messages still dispatch to the real app"
                Expect.equal (AVal.force app.Model) 42 "The ordinary update reached the model"
                do! send stream token 8uy true closePayload
                do! checkFrame stream token 8uy Array.empty
                do! drained.Task.WaitAsync token
                Expect.isEmpty errors "Normal Ping/text/Close handling reports no application or connection errors"
                Expect.equal guard.Calls 0 "Events without scenes never use the rendering runtime"
                Expect.equal (replies |> Seq.map Array.toList |> Seq.toList) (payloads |> Array.map Array.toList |> Array.toList)
                    "Every wire Pong preserves the complete empty binary maximum-size and subsequent short Ping payload"
            }
            check.GetAwaiter().GetResult()
        finally
            client.Dispose()
            if entered.Task.IsCompleted then drained.Task.WaitAsync(TimeSpan.FromSeconds 5.0).GetAwaiter().GetResult()

    type private RecordingSocket(messages: (WebSocketOpCode * byte[])[]) =
        let results = messages |> Array.map (fst >> Task.FromResult)
        let sent = ResizeArray<WebSocketOpCode * byte[] * bool * CancellationToken * int64>()
        let mutable reads = 0
        let mutable closes = 0
        let mutable lastBuffer = Array.empty<byte>
        let mutable allocatedAtReceive = 0L
        let mutable receiveThread = 0
        member _.Sent = sent.ToArray()
        member _.Reads = reads
        member _.Closes = closes
        member _.Buffer = lastBuffer
        interface IWebSocket with
            member _.Receive(buffer, token) =
                token.ThrowIfCancellationRequested()
                Expect.equal buffer.Position 0 "The application resets its receive position"
                let index = reads
                reads <- reads + 1
                let _, payload = messages.[index]
                Array.Fill(buffer.Array, 0xC7uy)
                buffer.Write payload
                lastBuffer <- buffer.Array
                receiveThread <- Environment.CurrentManagedThreadId
                allocatedAtReceive <- GC.GetAllocatedBytesForCurrentThread()
                results.[index]
            member _.Send(opcode, data, token, final) =
                let allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedAtReceive
                Expect.equal Environment.CurrentManagedThreadId receiveThread "Completed fixture tasks keep the allocation observation on one thread"
                sent.Add(opcode, data, final, token, allocated)
                Task.CompletedTask
            member _.Close token =
                token.ThrowIfCancellationRequested()
                closes <- closes + 1
                Task.CompletedTask
            member _.Dispose() = ()

    let private recordedClient messages =
        use cancellation = new CancellationTokenSource()
        let runtime, guard = rejectingRuntime()
        let app =
            { new IMutableApp with
                member _.UpdateLock = failwith "No image requests may enter the rendering lock"
                member _.CancellationToken = cancellation.Token
                member _.Register _ = failwith "RenderClient does not own app registrations"
                member _.Dispose() = failwith "RenderClient must not dispose its app" }
        let errors = ConcurrentQueue<exn>()
        let socket = new RecordingSocket(messages)
        let server =
            { runtime = runtime
              compressor = ValueNone
              getScene = fun _ -> failwith "No scene requests allowed"
              getState = fun _ -> failwith "No rendering state requests allowed"
              rendered = fun _ -> failwith "No frames rendered"
              onError = fun _ error -> errors.Enqueue error }
        let info =
            { id = { session = Guid.NewGuid(); elementId = "ping-fixture" }
              server = server; socket = socket; signature = Unchecked.defaultof<IFramebufferSignature>
              sceneName = "unused"; getScene = fun _ _ -> failwith "No scene compilation allowed"
              useMapping = false; targetQuality = RenderQuality.full }
        let client = RenderClient(app, info)
        let work = Task.Run(Func<Task>(fun () -> client.Run()))
        try
            work.WaitAsync(TimeSpan.FromSeconds 10.0).GetAwaiter().GetResult()
            Expect.equal socket.Reads messages.Length "All scripted messages, including Close, were received"
            Expect.equal socket.Closes 1 "Normal Close cleanup runs once"
            Expect.isEmpty errors "No socket/parser/rendering errors are hidden"
            Expect.equal guard.Calls 0 "No rendering runtime operation was attempted"
            let sent = socket.Sent
            for _, data, final, token, _ in sent do
                Expect.isTrue final "Pong messages remain final"
                Expect.equal token cancellation.Token "Preserve the app lifetime token"
                Expect.isFalse (Object.ReferenceEquals(data, socket.Buffer)) "Replies never retain the reusable receive buffer"
            sent, client.FrameCount
        finally
            cancellation.Cancel()
            work.WaitAsync(TimeSpan.FromSeconds 5.0).GetAwaiter().GetResult()

    let private renderPings payloads =
        let inputs = [|
            for payload in payloads do yield WebSocketOpCode.Ping, payload
            yield WebSocketOpCode.Text, Encoding.UTF8.GetBytes "#"
            yield WebSocketOpCode.Text, Encoding.UTF8.GetBytes "#ping with text and 東京"
            yield WebSocketOpCode.Text, Encoding.UTF8.GetBytes "{\"case\":\"Rendered\"}"
            yield WebSocketOpCode.Text, Encoding.UTF8.GetBytes "{\"case\":\"Rendered\"}"
            yield WebSocketOpCode.Close, closePayload
        |]
        let replies, frames = recordedClient inputs
        Expect.equal frames 2 "Ordinary Rendered messages retain their bookkeeping without requesting images"
        Expect.equal replies.Length (Array.length payloads + 2) "Only Ping and text heartbeat messages produce Pongs"
        for i in 0 .. replies.Length - 1 do
            let opcode, data, _, _, _ = replies.[i]
            Expect.equal opcode WebSocketOpCode.Pong "RenderClient replies with Pong"
            if i >= Array.length payloads then Expect.isTrue (Object.ReferenceEquals(data, Array.empty<byte>)) "Text heartbeats retain the empty SendPong helper"
        let expected = Array.append payloads [| Array.empty; Array.empty |]
        Expect.equal (replies |> Array.map (fun (_, data, _, _, _) -> Array.toList data)) (expected |> Array.map Array.toList)
            "All retained replies preserve exactly the bytes despite later receive-buffer reuse"

    [<Tests>]
    let tests =
        let prefixes = [Array.empty; [| 0xC1uy; 0xA5uy; 0uy |]]
        let backendTests (backend: Backend) =
            testList $"{backend.Name}.Frames" [
                test "Controls do not finish or alter fragmented data" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do
                            for control, data in [9uy, [| 0uy; 0xFFuy; 0x23uy |]; 9uy, Array.empty; 10uy, [| 0x42uy; 0uy |]] do
                                fragmented backend opcode control data prefix
                }
                test "Close discards incomplete data" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do closeDuringFragments backend opcode prefix
                }
                test "Ordinary messages and standalone controls retain delivery" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do
                            for fragments in [false; true] do ordinary backend opcode fragments prefix
                        standalone backend prefix
                }
            ]
        testList "Core.WebSocket" [
            backendTests Suave
            backendTests Giraffe
            testList "ApplicationPing" [
                test "Suave events echo standalone Ping payloads without buffer tails" { eventPings pings }
                test "Suave events retain empty Ping text heartbeat dispatch and Close behavior" { eventPings [| Array.empty |] }
                test "RenderClient echoes standalone Ping payloads without rendering" { renderPings pings }
                test "RenderClient retains empty Ping text heartbeat messages and Close behavior" { renderPings [| Array.empty |] }
            ]
        ]