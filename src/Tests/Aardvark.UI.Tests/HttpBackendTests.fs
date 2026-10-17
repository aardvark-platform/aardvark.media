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

    module private CustomResources =
        let inheritedMime = "application/x-inherited"
        let indexPath = "/custom-index/index.html"

        type Case = { Resource : HttpResource; Url : string; ContentType : string }

        let private create group file mime contentType =
            { Resource = { Path = $"custom-mime\\{group}\\{file}"; MimeType = mime }
              Url = $"/custom-mime/{group}/{file}"; ContentType = contentType }

        let inferred = [
            for group, mime in ["null", null; "empty", ""] do
                for file, expected in [
                    "script.js", "text/javascript"; "script.JS", "text/javascript"
                    "style.css", "text/css"; "style.CSS", "text/css"
                    "note.txt", "text/plain"; "note.TXT", "text/plain"
                    "image.svg", "image/svg+xml"; "image.SVG", "image/svg+xml"
                ] do
                    yield create group file mime expected
        ]

        let controls = [
            for group, mime in ["null", null; "empty", ""] do
                yield create group "unknown.aardvark-test-unknown" mime inheritedMime
                yield create group "extensionless" mime inheritedMime
            for file, mime in [
                "override.js", "application/x-explicit; charset=us-ascii"
                "override.CSS", "text/x-css"
                "override.aardvark-test-unknown", "image/x-explicit"
                "override", "application/x-no-extension"
            ] do
                yield create "explicit" file mime mime
        ]

        let routes (http: IHttpBackend<'Context, 'Handler>) =
            let (>=>) x y = http.compose x y
            let asm = typeof<TestServer>.Assembly
            let chooser resource name =
                if name = asm.GetName().Name + ".embedded_file.txt" then Some resource else None
            [
                for entry in inferred @ controls do
                    // Only matching routes receive the inherited header; siblings stay untouched.
                    http.route entry.Url >=> http.mimeType inheritedMime >=> http.assemblyWith (chooser entry.Resource) asm
                http.choose [http.route "/"; http.route indexPath]
                    >=> http.assemblyWith (chooser { Path = "custom-index\\index.html"; MimeType = "text/x-index" }) asm
                http.subRoute "/custom-excluded" (http.choose [
                    http.assemblyWith (fun _ -> None) asm
                    http.notFound "Excluded"
                ])
            ]

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

            yield! CustomResources.routes http
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

        let private resourceBytes () =
            let asm = typeof<TestServer>.Assembly
            use stream = asm.GetManifestResourceStream(asm.GetName().Name + ".embedded_file.txt")
            Stream.readAllBytes stream

        let private checkResource (client: HttpClient) (server: TestServer) path contentType =
            use response = client.GetAsync($"http://{server.Host}{path}", server.CancellationToken).Result
            Expect.equal response.StatusCode HttpStatusCode.OK $"{path}: resource status"
            Expect.sequenceEqual (response.Content.ReadAsByteArrayAsync().Result) (resourceBytes()) $"{path}: exact manifest bytes"
            Expect.equal (string response.Content.Headers.ContentType) contentType $"{path}: complete Content-Type"

        let customResourceMimeFallback (client: HttpClient) (server: TestServer) =
            for entry in CustomResources.inferred do
                checkResource client server entry.Url entry.ContentType

        let customResourceMimeControls (client: HttpClient) (server: TestServer) =
            for entry in CustomResources.controls do
                checkResource client server entry.Url entry.ContentType

        let customResourceRouting (client: HttpClient) (server: TestServer) =
            for prefix in [None; Some "/proxy/ui"] do
                use request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"http://{server.Host}/")
                prefix |> Option.iter (fun value -> request.Headers.Add("GLOBAL_PATH", value))
                use response = client.SendAsync(request, server.CancellationToken).Result
                Expect.equal response.StatusCode HttpStatusCode.MovedPermanently "Index alias retains its permanent redirect"
                Expect.equal (response.Headers.GetValues("Location") |> Seq.toList)
                    [(prefix |> Option.defaultValue "") + CustomResources.indexPath] "Index redirect retains its normalized served path"
            checkResource client server CustomResources.indexPath "text/x-index"
            use excluded = client.GetAsync($"http://{server.Host}/custom-excluded/embedded_file.txt", server.CancellationToken).Result
            Expect.equal excluded.StatusCode HttpStatusCode.NotFound "Chooser exclusions are not served"
            Expect.equal (excluded.Content.ReadAsStringAsync().Result) "Excluded" "Excluded resources reach the scoped fallback"
            checkResource client server "/embedded_file.txt" "text/plain"

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
                "Custom resource MIME fallback", Cases.customResourceMimeFallback
                "Custom resource MIME controls", Cases.customResourceMimeControls
                "Custom resource routing controls", Cases.customResourceRouting
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
        ]