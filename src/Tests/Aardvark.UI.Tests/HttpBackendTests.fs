namespace Aardvark.UI.Tests

open System
open System.IO
open System.Net
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.UI
open System.Net.Http
open Expecto

module ``HttpBackend Tests`` =

    type Task with
        member this.WaitIgnoreCancel() =
            try this.GetAwaiter().GetResult()
            with
            | :? OperationCanceledException -> ()
            | _ -> reraise()

    [<AbstractClass>]
    type TestServer(name: string, start: int -> CancellationToken -> Task) =
        let mutable port = 0
        let mutable tcs : CancellationTokenSource = null
        let mutable server = Task.CompletedTask
        let mutable refCount = 0

        member _.Name = name
        member this.Host = lock this (fun _ -> $"localhost:{port}")
        member this.CancellationToken = lock this (fun _ -> tcs.Token)
        member this.Acquire() =
            lock this (fun _ ->
                inc &refCount
                if refCount = 1 then
                    port <- Server.getFreeTcpPort IPAddress.Loopback
                    tcs <- new CancellationTokenSource()
                    server <- start port tcs.Token
            )

            let release() =
                lock this (fun _ ->
                    dec &refCount
                    if refCount = 0 then
                        tcs.Cancel()
                        server.WaitIgnoreCancel()
                        tcs.Dispose()
                        port <- 0
                        tcs <- null
                        server <- Task.CompletedTask
                )

            { new IDisposable with
                member _.Dispose() = release() }

    type GiraffeTestServer(content) =
        inherit TestServer("Giraffe", fun p t -> Giraffe.Server.startLocalhost p t (content t))

    type SuaveTestServer(content) =
        inherit TestServer("Suave", fun p t -> Suave.Server.startLocalhost p t (content t))

    type JsonInput =
        { Foo : string; Bar : int }
        member this.Count = this.Foo.Length + this.Bar

    type JsonOutput =
        { Count : int }

    module private MethodFilters =
        let private standardMethods = ["DELETE"; "GET"; "HEAD"; "OPTIONS"; "PATCH"; "POST"; "PUT"; "TRACE"]
        let private extensionMethods = ["QUERY"; "PURGE"; "X-CUSTOM"]
        let methods = standardMethods @ extensionMethods

    let private testContent (http: IHttpBackend<'HttpContext, 'HttpHandler>) (cancellationToken: CancellationToken) =
        let (>=>) x y = http.compose x y

        let returnQueryParam name =
            http.request (fun r ->
                let value = r.QueryParam name |> Option.defaultValue ""
                http.text value
            )

        let returnQueryParams =
            http.request (fun r ->
                r.QueryParams
                |> Seq.map (fun (KeyValue(name, values)) -> $"{name}:{values}")
                |> String.concat ";"
                |> http.text
            )

        let returnHeader name =
            http.request (fun r ->
                let value = r.Header name |> Option.defaultValue ""
                http.text value
            )

        let redirect =
            http.request (fun r ->
                let permanent = r.QueryParam "permanent" |> Option.get |> Boolean.Parse
                let location = r.QueryParam "location" |> Option.get
                http.header "X-Redirect" "preserved"
                >=> http.redirectTo permanent location
            )

        let sendFile =
            http.request (fun r ->
                http.status 202
                >=> http.mimeType "application/octet-stream"
                >=> http.header "X-Send-File" "preserved"
                >=> http.sendFile (r.QueryParam "path" |> Option.get)
            )

        let webSocket reportCapacity =
            http.handShake (fun socket _ ->
                let buffer = SocketBuffer(128)
                let mutable running = true

                task {
                    while running && not cancellationToken.IsCancellationRequested do
                        buffer.Position <- 0
                        match! socket.Receive(buffer, cancellationToken) with
                        | (WebSocketOpCode.Text | WebSocketOpCode.Binary) as opcode ->
                            if reportCapacity then
                                do! socket.Send(opcode, buffer.Data.ToArray(), cancellationToken)
                                do! socket.SendText(string buffer.Size, cancellationToken)
                            elif opcode = WebSocketOpCode.Text then
                                let response = $"Received: {buffer.DataUtf8}"
                                do! socket.SendText(response, cancellationToken)

                        | WebSocketOpCode.Close ->
                            do! socket.Close(cancellationToken)
                            running <- false

                        | _ ->
                            ()
                }
            )

        [
            http.route    "/text"           >=> http.text "Hello World!"
            http.route    "/html"           >=> http.html "<html lang=\"\"><body/></html>"
            http.routef   "/status/%i"      http.status
            http.route    "/redirect"       >=> redirect
            http.route    "/redirect-relative" >=> http.header "X-Redirect" "preserved"
                                              >=> http.redirectRelative "/text?q=hello%20world#details"
            http.subRoute "/sub"            (http.choose [http.routef "/%s" http.text])
            http.routef   "/query-param/%s" returnQueryParam
            http.route    "/query-params"   >=> returnQueryParams
            http.routef   "/header/%s"      returnHeader
            http.route    "/method"         >=> http.request (_.Method >> http.text)
            http.routef   "/path/%s"        (fun _ -> http.request (_.Path >> http.text))
            http.route    "/body"           >=> http.bindBody (http.text: byte[] -> _)
            http.route    "/send"           >=> http.sendFile "test_file.txt"
            http.route    "/send-bytes"     >=> sendFile
            http.route    "/json"           >=> http.mapJson (fun (input: JsonInput) -> { Count = input.Count })
            http.route    "/ws"             >=> webSocket false
            http.route    "/ws-buffer"      >=> webSocket true

            for method in MethodFilters.methods do
                http.route $"/method-filter/{method}" >=> http.method method >=> http.status 202

            http.assembly typeof<TestServer>.Assembly
            http.notFound "Not found"
        ]

    module Cases =

        let text (client: HttpClient) (server: TestServer) =
            let r = client.GetAsync($"http://{server.Host}/text").Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            Expect.equal r.Content.Headers.ContentType.MediaType "text/plain" "Unexpected content type"

            let result = r.Content.ReadAsStringAsync().Result
            Expect.equal result "Hello World!" "Unexpected result"

        let html (client: HttpClient) (server: TestServer) =
            let r = client.GetAsync($"http://{server.Host}/html").Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            Expect.equal r.Content.Headers.ContentType.MediaType "text/html" "Unexpected content type"

            let result = r.Content.ReadAsStringAsync().Result
            Expect.stringStarts result "<html" "Unexpected result"

        let status (client: HttpClient) (server: TestServer) =
            let test (status: HttpStatusCode) =
                let r = client.GetAsync($"http://{server.Host}/status/{int status}").Result
                Expect.equal r.StatusCode status "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result "" "Unexpected result"

            test HttpStatusCode.OK
            test HttpStatusCode.Accepted
            test HttpStatusCode.Ambiguous

        let private checkRedirect permanent location (response: HttpResponseMessage) (server: TestServer) =
            let status, reason =
                if permanent then HttpStatusCode.MovedPermanently, "Moved Permanently"
                else HttpStatusCode.Found, "Found"

            Expect.equal response.StatusCode status $"Unexpected redirect status: {location}"
            Expect.equal response.ReasonPhrase reason "Unexpected redirect reason phrase"
            Expect.equal (response.Headers.GetValues("Location") |> Seq.toList) [location] "Changed redirect destination"
            Expect.equal (response.Headers.GetValues("X-Redirect") |> Seq.toList) ["preserved"] "Lost response header"

            let expectedBody =
                if server :? SuaveTestServer then
                    Expect.equal (string response.Content.Headers.ContentType) "text/html; charset=utf-8" "Changed redirect content type"
                    // Keep Suave's standard redirect body even when overriding its status to 301.
                    let expected =
                        global.Suave.Redirection.redirect location global.Suave.Http.HttpContext.empty
                        |> Async.RunSynchronously
                        |> Option.get

                    match expected.response.content with
                    | global.Suave.Http.Bytes data -> data
                    | _ -> failwith "Expected Suave's standard redirect to return bytes"
                else
                    Array.empty

            let body = response.Content.ReadAsByteArrayAsync().Result
            Expect.sequenceEqual body expectedBody "Changed redirect body"
            Expect.equal response.Content.Headers.ContentLength (Nullable(int64 expectedBody.Length)) "Changed redirect content length"

        let redirect permanent (client: HttpClient) (server: TestServer) =
            for destination in ["text"; "/text"; $"http://{server.Host}/text"] do
                for suffix in [""; "?q=hello%20world&next=%2Fother#details"] do
                    let location = destination + suffix
                    let url = $"http://{server.Host}/redirect?permanent={permanent}&location={Uri.EscapeDataString location}"
                    use response = client.GetAsync(url).Result
                    checkRedirect permanent location response server

        let redirectRelative (client: HttpClient) (server: TestServer) =
            for globalPath, prefix in [Some "/proxy/ui", "/proxy/ui"; Some "/proxy/ui/", "/proxy/ui"; None, ""] do
                use request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"http://{server.Host}/redirect-relative")
                globalPath |> Option.iter (fun value -> request.Headers.Add("GLOBAL_PATH", value))
                use response = client.SendAsync(request).Result
                checkRedirect true (prefix + "/text?q=hello%20world#details") response server

        let subRoute (client: HttpClient) (server: TestServer) =
            let test (path: string) =
                let r = client.GetAsync($"http://{server.Host}/sub/{path}").Result
                Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result path "Unexpected result"

            test "hello"
            test "friend"

        let queryParam (client: HttpClient) (server: TestServer) =
            let test (name: string) (queryParams: (string * string option) list) =
                let query =
                    ("", queryParams) ||> List.fold (fun s (n, v) ->
                        let sep = if String.isEmpty s then "?" else "&"
                        let v = v |> Option.defaultValue ""
                        $"{s}{sep}{n}={v}"
                    )

                let expected =
                    queryParams
                    |> List.tryPick (fun (n, v) -> if n = name then v else None)
                    |> Option.defaultValue ""

                let r = client.GetAsync($"http://{server.Host}/query-param/{name}{query}").Result
                Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result expected "Unexpected result"

            test "a" ["a", Some "1"; "b", None; "c", None]
            test "a" ["a", Some "1"; "a", Some "2"; "b", None; "c", None]
            test "a" ["a", None; "b", None; "c", None]

        let queryParams (client: HttpClient) (server: TestServer) =
            let test (queryParams: (string * string option) list) =
                let query =
                    ("", queryParams) ||> List.fold (fun s (n, v) ->
                        let sep = if String.isEmpty s then "?" else "&"
                        let v = v |> Option.defaultValue ""
                        $"{s}{sep}{n}={v}"
                    )

                let queryParams =
                    (Map.empty, queryParams) ||> List.fold (fun map (n, v) ->
                        let o = map |> Map.tryFind n |> Option.defaultValue []
                        let v = Option.toList v
                        map |> Map.add n (o @ v)
                    )

                let expected =
                    queryParams
                    |> Seq.map (fun (KeyValue(name, values)) -> $"{name}:{values}")
                    |> String.concat ";"

                let r = client.GetAsync($"http://{server.Host}/query-params{query}").Result
                Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result expected "Unexpected result"

            test ["a", Some "1"; "b", None; "c", Some "3"]
            test ["a", Some "1"; "a", Some "2"; "b", None; "c", None]
            test ["c", Some "42"; "a", None; "b", None; "c", Some "4"]

        let header (client: HttpClient) (server: TestServer) =
            let r = client.GetAsync($"http://{server.Host}/header/Host").Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            let result = r.Content.ReadAsStringAsync().Result
            Expect.equal result server.Host "Unexpected result"

        let method (client: HttpClient) (server: TestServer) =
            let test (method: string) =
                let url = $"http://{server.Host}/method"
                let r =
                    match method with
                    | HttpMethod.Get -> client.GetAsync(url).Result
                    | HttpMethod.Put -> client.PutAsync(url, null).Result
                    | _ -> failwith $"Invalid method: {method}"

                Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result method "Unexpected result"

            test HttpMethod.Get
            test HttpMethod.Put

        let methodFilters (client: HttpClient) (server: TestServer) =
            let test (configuredMethod: string) (requestMethod: string) =
                let url = $"http://{server.Host}/method-filter/{configuredMethod}"
                let r =
                    use msg = new HttpRequestMessage(HttpMethod requestMethod, url)
                    client.Send msg

                let expected = if requestMethod = configuredMethod then HttpStatusCode.Accepted else HttpStatusCode.NotFound
                Expect.equal r.StatusCode expected "Unexpected status code"

            for configuredMethod in MethodFilters.methods do
                for requestMethod in MethodFilters.methods do
                    test configuredMethod requestMethod

        let path (client: HttpClient) (server: TestServer) =
            let test (path: string) =
                let r = client.GetAsync($"http://{server.Host}/path/{path}").Result
                Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                let result = r.Content.ReadAsStringAsync().Result
                Expect.equal result $"/path/{path}" "Unexpected result"

            test "foo.html"
            test "bar.json"

        let body (client: HttpClient) (server: TestServer) =
            use content = new StringContent("Foobar")
            let r = client.PostAsync($"http://{server.Host}/body", content).Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            let result = r.Content.ReadAsStringAsync().Result
            Expect.equal result "Foobar" "Unexpected result"

        let sendFile (client: HttpClient) (server: TestServer) =
            let r = client.GetAsync($"http://{server.Host}/send").Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            let result = r.Content.ReadAsStringAsync().Result
            Expect.equal result "Hello!" "Unexpected result"

        let sendFileBytes (method: System.Net.Http.HttpMethod) (client: HttpClient) (server: TestServer) =
            let withBom (encoding: Encoding) =
                Array.append (encoding.GetPreamble()) (encoding.GetBytes "Hello, π!")

            // Suave's sendFile sets 200; Giraffe retains the supplied status.
            let expectedStatus = if server :? GiraffeTestServer then HttpStatusCode.Accepted else HttpStatusCode.OK

            let files =
                [
                    "binary", Array.init 256 byte
                    "UTF-8 BOM", withBom Encoding.UTF8
                    "UTF-16 BOM", withBom Encoding.Unicode
                    "empty", Array.empty
                ]

            for name, data in files do
                let relativePath = $"send_file_{Guid.NewGuid():N}.bin"
                let absolutePath = Path.GetFullPath relativePath

                try
                    File.WriteAllBytes(absolutePath, data)

                    for path in [relativePath; absolutePath] do
                        let context = $"{name}, {method}, {path}"
                        use request = new HttpRequestMessage(method, $"http://{server.Host}/send-bytes?path={Uri.EscapeDataString path}")
                        use response = client.SendAsync(request).Result
                        Expect.equal response.StatusCode expectedStatus $"Unexpected status: {context}"
                        Expect.equal response.Content.Headers.ContentType.MediaType "application/octet-stream" $"Unexpected content type: {context}"
                        Expect.equal (response.Headers.GetValues("X-Send-File") |> Seq.toList) ["preserved"] $"Unexpected header: {context}"

                        let expected = if method = System.Net.Http.HttpMethod.Head then Array.empty else data
                        let result = response.Content.ReadAsByteArrayAsync().Result
                        Expect.sequenceEqual result expected $"Unexpected bytes: {context}"
                        Expect.equal response.Content.Headers.ContentLength (Nullable(int64 data.Length)) $"Unexpected length: {context}"
                finally
                    File.Delete absolutePath

        let json (client: HttpClient) (server: TestServer) =
            let data = { Foo = "Hello"; Bar = 4 }
            use content = new StringContent(Pickler.jsonToString data)
            let r = client.PostAsync($"http://{server.Host}/json", content).Result
            Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
            let result = r.Content.ReadAsStringAsync().Result |> Pickler.unpickleOfJson<JsonOutput>
            Expect.equal result.Count data.Count "Unexpected result"

        let webSocket (_: HttpClient) (server: TestServer) =
            use ws = new ClientWebSocket()
            ws.ConnectAsync(Uri $"ws://{server.Host}/ws", server.CancellationToken).Wait()

            let text = "Hello World!"
            let data = Encoding.UTF8.GetBytes text
            ws.SendAsync(data.AsMemory(), WebSocketMessageType.Text, true, server.CancellationToken).AsTask().Wait 2000
                |> flip Expect.isTrue "Timed out waiting for send"

            let receive =
                task {
                    use inputStream = new MemoryStream()
                    let buffer = Array.zeroCreate<byte> 128
                    let mutable endOfMessage = false

                    while not endOfMessage do
                        let! result = ws.ReceiveAsync(buffer.AsMemory(), server.CancellationToken)
                        if result.MessageType <> WebSocketMessageType.Text then
                            failwith $"Unexpected message: {result.MessageType}"
                        inputStream.Write(buffer, 0, result.Count)
                        endOfMessage <- result.EndOfMessage

                    return Encoding.UTF8.GetString(inputStream.ToArray())
                }

            receive.Wait 4000 |> flip Expect.isTrue "Timed out waiting for receive"
            Expect.equal receive.Result $"Received: {text}" "Unexpected received message"

            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", server.CancellationToken).Wait 2000
                |> flip Expect.isTrue "Timed out waiting for close"

        let webSocketReassembly (messageType: WebSocketMessageType) (_: HttpClient) (server: TestServer) =
            use timeout = CancellationTokenSource.CreateLinkedTokenSource(server.CancellationToken)
            timeout.CancelAfter(TimeSpan.FromSeconds 15.0)
            let token = timeout.Token

            let seed =
                if messageType = WebSocketMessageType.Text then Encoding.UTF8.GetBytes "héllo π world!"
                else Array.init 16 (fun i -> byte (i * 17))

            for length, fragmentSize in [16, 16; 128, 128; 512, 512; 16, 1; 128, 8; 512, 32] do
                use ws = new ClientWebSocket()

                let receive expectedType =
                    task {
                        use stream = new MemoryStream()
                        let buffer = Array.zeroCreate<byte> 128
                        let mutable finished = false

                        while not finished do
                            let! result = ws.ReceiveAsync(buffer.AsMemory(), token)
                            Expect.equal result.MessageType expectedType "Unexpected WebSocket opcode"
                            stream.Write(buffer, 0, result.Count)
                            finished <- result.EndOfMessage

                        return stream.ToArray()
                    }

                let check (data: byte[]) fragmentSize =
                    task {
                        for offset in 0 .. fragmentSize .. data.Length - 1 do
                            let count = min fragmentSize (data.Length - offset)
                            let last = offset + count = data.Length
                            do! ws.SendAsync(data.AsMemory(offset, count), messageType, last, token)

                        let context = $"{messageType}, {data.Length} bytes, {fragmentSize}-byte fragments"
                        let! response = receive messageType
                        Expect.sequenceEqual response data $"Unexpected reassembled payload: {context}"
                        let! capacity = receive WebSocketMessageType.Text
                        let capacity = capacity |> Encoding.UTF8.GetString |> Int32.Parse
                        Expect.equal capacity (max 128 length) $"Unexpected server buffer capacity: {context}"
                    }

                let run =
                    task {
                        do! ws.ConnectAsync(Uri $"ws://{server.Host}/ws-buffer", token)
                        let payload = Array.init length (fun i -> seed.[i % seed.Length])
                        do! check payload fragmentSize
                        // Reuse the buffer for a shorter message without retaining the previous payload.
                        do! check (Encoding.UTF8.GetBytes "reuse") 5
                        do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token)
                    }

                run.GetAwaiter().GetResult()

        let embeddedResources (client: HttpClient) (server: TestServer) =
            let asm = typeof<TestServer>.Assembly

            let test (exists: bool) (resourceName: string) (path: string) =
                let expected =
                    use s = asm.GetManifestResourceStream resourceName
                    if isNull s then failwith $"Embedded resource {resourceName} does not exist"
                    s |> Stream.readAllBytes |> Encoding.UTF8.GetString

                let r = client.GetAsync($"http://{server.Host}/{path}").Result

                if exists then
                    let contentType =
                        path
                        |> Path.GetExtension
                        |> MimeType.ofFileExtension
                        |> Option.defaultValue ""

                    Expect.equal r.StatusCode HttpStatusCode.OK $"Unexpected status code for {path}"
                    Expect.equal r.Content.Headers.ContentType.MediaType contentType $"Unexpected content type for {path}"
                    let result = r.Content.ReadAsStringAsync().Result
                    Expect.equal result expected $"Unexpected result for {path}"
                else
                    Expect.equal r.StatusCode HttpStatusCode.NotFound $"Unexpected status code for {path}"

            let root = asm.GetName().Name
            test true $"{root}.embedded_file.txt" "embedded_file.txt"
            test true $"{root}.Resources.embedded_resource_1.txt" "resources/embedded_resource_1.txt"
            test true $"RESOURCES{Path.DirectorySeparatorChar}embedded_resource_2.md" "resources/embedded_resource_2.md"
            test false "embedded_file_incorrect.txt" "embedded_file_incorrect.txt"

    module private SuaveFrames =

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

        let private run (prefix: byte[]) (check: System.Net.Sockets.NetworkStream -> CancellationToken -> TaskCompletionSource<WebSocketOpCode * byte[] * int>[] -> Task<unit>) =
            let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let drained = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let messages = Array.init 3 (fun _ -> TaskCompletionSource<WebSocketOpCode * byte[] * int>(TaskCreationOptions.RunContinuationsAsynchronously))
            let http = Suave.HttpBackend.Instance
            let server = SuaveTestServer(fun token -> [
                http.handShake (fun socket _ ->
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
                                let reply = if opcode = WebSocketOpCode.Ping then WebSocketOpCode.Pong else opcode
                                do! socket.Send(reply, response, token)
                                running <- opcode <> WebSocketOpCode.Close
                        finally
                            drained.TrySetResult() |> ignore
                    })
            ])
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

        let private finish stream token messages index prefix =
            task {
                do! send stream token 8uy true closePayload
                do! checkFrame stream token 8uy closePayload
                let! _ = delivered messages index token WebSocketOpCode.Close prefix closePayload
                return ()
            }

        let private subsequent stream token messages opcode prefix capacity =
            task {
                let opcode = if opcode = 1uy then 2uy else 1uy
                let next = Encoding.UTF8.GetBytes "next\000"
                do! send stream token opcode true next
                do! checkFrame stream token opcode next
                let! nextCapacity = delivered messages 1 token (dataOpcode opcode) prefix next
                Expect.equal nextCapacity capacity "Shorter message reuses buffer capacity"
                do! finish stream token messages 2 prefix
            }

        let fragmented opcode control (controlPayload: byte[]) prefix =
            run prefix (fun stream token messages -> task {
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
                Expect.equal capacity (2 * (prefix.Length + data.Length - 1)) "Only data grows the buffer"
                do! subsequent stream token messages opcode prefix capacity
            })

        let closeDuringFragments opcode prefix =
            run prefix (fun stream token messages -> task {
                do! send stream token opcode false (payload opcode).[..1]
                do! send stream token 0uy false (payload opcode).[2..10]
                do! send stream token 8uy true closePayload
                let! _ = delivered messages 0 token WebSocketOpCode.Close prefix closePayload
                do! checkFrame stream token 8uy closePayload
                Expect.isFalse messages.[1].Task.IsCompleted "Incomplete data is never dispatched"
            })

        let ordinary opcode fragmented prefix =
            run prefix (fun stream token messages -> task {
                let data = payload opcode
                if fragmented then
                    do! send stream token opcode false data.[..1]
                    do! send stream token 0uy false data.[2..]
                    do! send stream token 0uy true Array.empty
                else do! send stream token opcode true data
                do! checkFrame stream token opcode data
                let! capacity = delivered messages 0 token (dataOpcode opcode) prefix data
                Expect.equal capacity (prefix.Length + data.Length) "Ordinary append and growth"
                do! subsequent stream token messages opcode prefix capacity
            })

        let standalone prefix =
            let controls = [|
                9uy, WebSocketOpCode.Ping, [| 0uy; 0xFFuy; 0x19uy |]
                10uy, WebSocketOpCode.Pong, Array.empty
            |]
            run prefix (fun stream token messages -> task {
                for index in 0 .. controls.Length - 1 do
                    let wireOpcode, opcode, data = controls.[index]
                    do! send stream token wireOpcode true data
                    let! _ = delivered messages index token opcode prefix data
                    do! checkFrame stream token 10uy data
                do! finish stream token messages 2 prefix
            })

        let tests =
            let prefixes = [Array.empty; [| 0xC1uy; 0xA5uy; 0uy |]]
            testList "Suave masked frames" [
                test "Controls do not finish or alter fragmented data" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do
                            for control, data in [9uy, [| 0uy; 0xFFuy; 0x23uy |]; 9uy, Array.empty; 10uy, [| 0x42uy; 0uy |]] do
                                fragmented opcode control data prefix
                }
                test "Close discards incomplete data but preserves its payload" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do closeDuringFragments opcode prefix
                }
                test "Ordinary messages and standalone controls retain delivery" {
                    for prefix in prefixes do
                        for opcode in [1uy; 2uy] do
                            for fragments in [false; true] do ordinary opcode fragments prefix
                        standalone prefix
                }
            ]

    [<Tests>]
    let tests =
        let cases =
            [
                "Text",               Cases.text
                "HTML",               Cases.html
                "Status",             Cases.status
                "Redirect temporary", Cases.redirect false
                "Redirect permanent", Cases.redirect true
                "Redirect relative",  Cases.redirectRelative
                "Sub route",          Cases.subRoute
                "Query parameter",    Cases.queryParam
                "Query parameters",   Cases.queryParams
                "Header",             Cases.header
                "Method",             Cases.method
                "Method filters",     Cases.methodFilters
                "Path",               Cases.path
                "Body",               Cases.body
                "Send file",          Cases.sendFile
                "Send file bytes",    Cases.sendFileBytes System.Net.Http.HttpMethod.Get
                "Send file HEAD",     Cases.sendFileBytes System.Net.Http.HttpMethod.Head
                "JSON",               Cases.json
                "WebSocket",          Cases.webSocket
                "WebSocket text reassembly",   Cases.webSocketReassembly WebSocketMessageType.Text
                "WebSocket binary reassembly", Cases.webSocketReassembly WebSocketMessageType.Binary
                "Embedded resources", Cases.embeddedResources
            ]

        let createTests (useGiraffe: bool) =
            let server : TestServer =
                if useGiraffe then
                    GiraffeTestServer(testContent Giraffe.HttpBackend.Instance)
                else
                    SuaveTestServer(testContent Suave.HttpBackend.Instance)

            let withServer f () =
                use _ = server.Acquire()
                f server

            cases
            |> List.map (fun (name, run) ->
                name, fun server ->
                    use client = new HttpClient(new HttpClientHandler(AllowAutoRedirect = false))
                    run client server
            )
            |> testFixture withServer
            |> List.ofSeq
            |> testList server.Name

        testList "HttpBackend Tests" [
            createTests true
            createTests false
            SuaveFrames.tests
        ]