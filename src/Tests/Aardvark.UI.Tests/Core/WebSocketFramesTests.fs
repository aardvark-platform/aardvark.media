namespace Aardvark.UI.Tests

open System
open System.Net
open System.Text
open System.Threading
open System.Threading.Tasks
open Aardvark.UI
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
            let connect = task {
                let request = Encoding.ASCII.GetBytes $"GET /frames HTTP/1.1\r\nHost: {server.Host}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"
                do! stream.WriteAsync(request.AsMemory(), token)
                let response = StringBuilder()
                while not (response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) do
                    let! data = readBytes stream token 1
                    response.Append(char data.[0]) |> ignore
                    Expect.isLessThan response.Length 8192 "Bounded upgrade response"
                Expect.stringStarts (response.ToString()) "HTTP/1.1 101" "WebSocket upgrade"
                Expect.stringContains (response.ToString()) "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=" "WebSocket accept key"
            }
            connect.GetAwaiter().GetResult()
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
        ]