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

        let webSocket =
            http.handShake (fun socket _ ->
                let buffer = SocketBuffer(128)
                let mutable running = true

                task {
                    while running && not cancellationToken.IsCancellationRequested do
                        match! socket.Receive(buffer, cancellationToken) with
                        | WebSocketOpCode.Text ->
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
            http.subRoute "/sub"            (http.choose [http.routef "/%s" http.text])
            http.routef   "/query-param/%s" returnQueryParam
            http.route    "/query-params"   >=> returnQueryParams
            http.routef   "/header/%s"      returnHeader
            http.route    "/method"         >=> http.request (_.Method >> http.text)
            http.routef   "/path/%s"        (fun _ -> http.request (_.Path >> http.text))
            http.route    "/body"           >=> http.bindBody (http.text: byte[] -> _)
            http.route    "/send"           >=> http.sendFile "test_file.txt"
            http.route    "/json"           >=> http.mapJson (fun (input: JsonInput) -> { Count = input.Count })
            http.route    "/ws"             >=> webSocket
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
            test true @"RESOURCES\embedded_resource_2.md" "resources/embedded_resource_2.md"
            test false "embedded_file_incorrect.txt" "embedded_file_incorrect.txt"

    [<Tests>]
    let tests =
        let cases =
            [
                "Text",               Cases.text
                "HTML",               Cases.html
                "Status",             Cases.status
                "Sub route",          Cases.subRoute
                "Query parameter",    Cases.queryParam
                "Query parameters",   Cases.queryParams
                "Header",             Cases.header
                "Method",             Cases.method
                "Path",               Cases.path
                "Body",               Cases.body
                "Send file",          Cases.sendFile
                "JSON",               Cases.json
                "WebSocket",          Cases.webSocket
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
                    use client = new HttpClient()
                    run client server
            )
            |> testFixture withServer
            |> List.ofSeq
            |> testList server.Name

        testList "HttpBackend Tests" [
            createTests true
            createTests false
        ]