namespace Aardvark.UI.Tests

open System
open System.IO
open System.Net
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Aardvark.Base
open Aardvark.UI
open System.Net.Http
open Expecto

module ``HttpBackend Tests`` =

    type JsonInput =
        { Foo : string; Bar : int }
        member this.Count = this.Foo.Length + this.Bar

    type JsonOutput =
        { Count : int }

    module private BodyUtf8 =
        let cases = [
            "empty", [||], ""
            "Unicode", Encoding.UTF8.GetBytes "hé π 東京 🦊", "hé π 東京 🦊"
            "BOM", [|0xEFuy;0xBBuy;0xBFuy;0x41uy|], "\uFEFFA"
            "repeated BOM", [|0xEFuy;0xBBuy;0xBFuy;0xEFuy;0xBBuy;0xBFuy|], "\uFEFF\uFEFF"
            "NUL", [|65uy;0uy;66uy;0uy|], "A\000B\000"
            "line endings", Encoding.UTF8.GetBytes "a\r\nb\rc\n", "a\r\nb\rc\n"
            "invalid", [|65uy;0xFFuy;66uy;0xC0uy;0xAFuy;67uy|], "A\uFFFDB\uFFFD\uFFFDC"
            "truncated", [|97uy;0xF0uy;0x9Fuy;0xA6uy|], "a\uFFFD"
            "stray continuation", [|0x80uy;0xBFuy|], "\uFFFD\uFFFD"
            "UTF16 BOM is not detected", [|0xFFuy;0xFEuy;65uy;0uy|], "\uFFFD\uFFFDA\000"
        ]
        let private timeout = TimeSpan.FromSeconds 5.0
        let private request (body : Stream) =
            { new IHttpRequest with
                member _.Path = "/"
                member _.Method = "POST"
                member _.Body = body
                member _.Header _ = None
                member _.Headers = Map.empty
                member _.QueryParam _ = None
                member _.QueryParams = Map.empty }

        type private AsyncBody(data : byte[], chunk : int, blocked : bool, failure : (exn * bool) option) =
            inherit Stream()
            let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable position = 0
            let mutable reads = 0
            let mutable syncReads = 0
            let mutable disposed = false
            let mutable eof = false
            let mutable destination : Stream = null
            do if not blocked then release.SetResult()
            let synchronousFailure () =
                match failure with Some(error,true) when position>0 -> raise error | _ -> ()
            let read (buffer : Memory<byte>) = task {
                reads <- reads+1
                entered.TrySetResult() |> ignore
                do! release.Task
                match failure with Some(error,_) when position>0 -> raise error | _ -> ()
                let count = min chunk (min buffer.Length (data.Length-position))
                data.AsMemory(position,count).CopyTo buffer
                position <- position+count
                if count=0 then eof <- true
                return count
            }
            member _.Entered = entered.Task
            member _.Release() = release.TrySetResult() |> ignore
            member _.Reads = reads
            member _.SyncReads = syncReads
            member _.Disposed = disposed
            member _.Eof = eof
            member _.Destination = destination
            override _.CanRead = not disposed
            override _.CanWrite = false
            override _.CanSeek = false
            override _.Length = failwith "Non-seekable body length"
            override _.Position with get() = failwith "Non-seekable body position" and set _ = failwith "Non-seekable body position"
            override _.Flush() = failwith "Unexpected flush"
            override _.Seek(_,_) = failwith "Unexpected seek"
            override _.SetLength _ = failwith "Unexpected SetLength"
            override _.Write(_,_,_) = failwith "Unexpected write"
            override _.Read(_,_,_) = syncReads <- syncReads+1; failwith "Synchronous body reads are forbidden"
            override _.ReadAsync(buffer,offset,count,_) = synchronousFailure(); read (buffer.AsMemory(offset,count))
            override _.ReadAsync(buffer : Memory<byte>,_) = synchronousFailure(); ValueTask<int>(read buffer)
            override this.CopyToAsync(target,bufferSize,token) =
                destination <- target
                base.CopyToAsync(target,bufferSize,token)
            override _.Dispose(disposing) = disposed <- true; release.TrySetResult() |> ignore; base.Dispose(disposing)

        let decode () =
            for name,data,expected in cases do
                use input = new MemoryStream(Array.concat [[|99uy;98uy|];data;[|97uy|]],2,data.Length,false,true)
                let req = request input
                Expect.equal (req.BodyUtf8.GetAwaiter().GetResult()) expected $"{name}: exact UTF-8 decoding including BOM/fallback"
                Expect.equal input.Position input.Length "Read through EOF from input origin"
                Expect.equal (req.BodyUtf8.GetAwaiter().GetResult()) "" "Repeated read at EOF is empty"
                Expect.isTrue input.CanRead "Caller still owns seekable stream"
                input.Position <- 0L
                Expect.sequenceEqual (req.BodyData.GetAwaiter().GetResult()) data "Byte reads remain exact"
                for chunk in [1;3] do
                    use stream = new AsyncBody(data,chunk,false,None)
                    let req = request stream
                    Expect.equal (req.BodyUtf8.WaitAsync(timeout).GetAwaiter().GetResult()) expected $"{name}: chunked async-only UTF-8"
                    Expect.equal (req.BodyUtf8.WaitAsync(timeout).GetAwaiter().GetResult()) "" "Nonseekable repeated read at EOF"
                    Expect.equal stream.SyncReads 0 "Never read synchronously"
                    Expect.isFalse stream.Disposed "Caller still owns nonseekable stream"
            use suffix = new MemoryStream(Encoding.UTF8.GetBytes "skip\uFEFFtail\000")
            suffix.Position <- 4L
            let req = request suffix
            Expect.equal (req.BodyUtf8.GetAwaiter().GetResult()) "\uFEFFtail\000" "Start at current position, retain later BOM and NUL"
            suffix.Position <- suffix.Length
            Expect.equal (req.BodyUtf8.GetAwaiter().GetResult()) "" "Explicit EOF is empty"

        let lifecycle () =
            let failures = ResizeArray<string>()
            for binding in [false;true] do
                for fault in 0..(if binding then 3 else 2) do
                    let error = IOException $"read/mapper sentinel {binding}/{fault}"
                    let readFailure = if fault=1 || fault=2 then Some((error :> exn),(fault=1)) else None
                    use input = new AsyncBody(Encoding.UTF8.GetBytes "\uFEFFhé\000\r\n",1,true,readFailure)
                    let mutable maps = 0
                    let mutable handlers = 0
                    let mutable mapped = ""
                    let work : Task =
                        if not binding then (request input).BodyUtf8 :> Task
                        else
                            let http = Giraffe.HttpBackend.Instance
                            let handler = http.bindBody (fun (body : string) ->
                                maps <- maps+1
                                Expect.isTrue input.Eof "Mapper runs only after complete asynchronous input"
                                Expect.isFalse input.Disposed "Mapper does not take ownership of request stream"
                                mapped <- body
                                if fault=3 then raise error
                                fun _ context -> handlers <- handlers+1; Task.FromResult(Some context))
                            Expect.equal input.Reads 0 "Constructing a binding does not access the request"
                            let context = DefaultHttpContext()
                            context.Request.Body <- input
                            handler (fun _ -> failwith "Unexpected downstream handler") context :> Task
                    try
                        Expect.isTrue (input.Entered.Wait timeout) "First actual ReadAsync is reached"
                        Expect.isFalse work.IsCompleted "Read stays incomplete while input is gated"
                        Expect.equal maps 0 "Mapper is deferred while reading"
                        input.Release()
                        let actual = try work.WaitAsync(timeout).GetAwaiter().GetResult(); None with e -> Some e
                        if fault=1 || fault=2 || (binding && fault=3) then
                            Expect.isTrue (actual |> Option.exists (fun e -> obj.ReferenceEquals(e,error))) "Read and mapper exception identity is preserved"
                        else Expect.isNone actual "Successful asynchronous read"
                        Expect.equal maps (if binding && fault<>1 && fault<>2 then 1 else 0) "Mapper runs once on success, never on read failure"
                        Expect.equal handlers (if binding && fault=0 then 1 else 0) "Mapped handler follows successful mapper only"
                        if binding && (fault=0 || fault=3) then Expect.equal mapped "\uFEFFhé\000\r\n" "Mapper receives exact decoded content"
                        Expect.isFalse input.Disposed "Request stream remains caller-owned after success or failure"
                        Expect.equal input.SyncReads 0 "Read failures do not trigger synchronous fallback"
                        if input.Destination.CanWrite then failures.Add $"{binding}/{fault}: owned copy stream was not disposed"
                    finally
                        input.Release()
                        try work.WaitAsync(timeout).GetAwaiter().GetResult() with _ -> ()
            Expect.isEmpty failures (String.concat "\n" failures)

        // Setup, assertions and input allocation stay outside the measured completed calls.
        let private allocation (input : MemoryStream) (req : IHttpRequest) =
            let mutable best = Int64.MaxValue
            let mutable retained = ""
            for _ in 1..3 do
                input.Position <- 0L
                let before = GC.GetAllocatedBytesForCurrentThread()
                retained <- req.BodyUtf8.GetAwaiter().GetResult()
                best <- min best (GC.GetAllocatedBytesForCurrentThread()-before)
            GC.KeepAlive retained
            best,retained
        let boundedAllocation () =
            let data = Array.create (2*1024*1024+17) 65uy
            use input = new MemoryStream(data,false)
            let allocated,text = allocation input (request input)
            Expect.equal text.Length data.Length "Entire ASCII body is decoded"
            Expect.isTrue (text |> Seq.forall ((=) 'A')) "Decoded result is consumed and correct"
            Expect.isTrue input.CanRead "Input remains owned by caller"
            let budget = 3L*int64 data.Length+65536L
            Expect.isLessThanOrEqual allocated budget $"Completed BodyUtf8 read allocated {allocated} B; budget {budget} B"

        let tests = testList "BodyUtf8" [
            test "Literal decoding current position and repeated async reads remain unchanged" { decode() }
            test "Async ownership errors and string mapper ordering remain intact" { lifecycle() }
            test "Large completed ASCII reads avoid a second byte copy" { boundedAllocation() }
        ]

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

            let indexRoute =
                http.choose [
                    http.route "/"
                    http.route indexPath
                ]
                >=> http.assemblyWith (chooser { Path = "custom-index\\index.html"; MimeType = "text/x-index" }) asm

            [
                for entry in inferred @ controls do
                    // Only matching routes receive the inherited header; siblings stay untouched.
                    http.route entry.Url >=> http.mimeType inheritedMime >=> http.assemblyWith (chooser entry.Resource) asm

                indexRoute
                http.subRoute "/custom-sub" indexRoute

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

        let redirectRel =
            http.header "X-Redirect" "preserved"
            >=> http.redirectRelative "/text?q=hello%20world#details"

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
            http.subRoute "/redirect-rel"   redirectRel
            http.subRoute "/redirect-r/"    redirectRel
            http.subRoute "/sub"            (http.choose [http.routef "/%s" http.text])
            http.routef   "/query-param/%s" returnQueryParam
            http.route    "/query-params"   >=> returnQueryParams
            http.routef   "/header/%s"      returnHeader
            http.route    "/method"         >=> http.request (_.Method >> http.text)
            http.routef   "/path/%s"        (fun _ -> http.request (_.Path >> http.text))
            http.route    "/body"           >=> http.bindBody (http.text: byte[] -> _)
            http.route    "/body-utf8"      >=> http.bindBody (http.text: string -> _)
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
            for dir in ["foo/bar"; "foo"; ""] do
                let url = $"http://{server.Host}/redirect-rel/{dir}"
                use response = client.GetAsync(url).Result
                checkRedirect true "/redirect-rel/text?q=hello%20world#details" response server

                let url = $"http://{server.Host}/redirect-r/{dir}"
                use response = client.GetAsync(url).Result
                checkRedirect true "/redirect-r/text?q=hello%20world#details" response server

            for dir in ["/foo/bar"; "/foo"; "/"; ""] do
                let url = $"http://{server.Host}/redirect-relxyz{dir}"
                use response = client.GetAsync(url).Result
                checkRedirect true "/redirect-rel/text?q=hello%20world#details" response server

                let url = $"http://{server.Host}/redirect-rxyz{dir}"
                use response = client.GetAsync(url).Result
                Expect.equal response.StatusCode HttpStatusCode.NotFound ""

        let subRoute (client: HttpClient) (server: TestServer) =
            let test (expected: string) (path: string) =
                let r = client.GetAsync($"http://{server.Host}/{path}").Result
                if notNull expected then
                    Expect.equal r.StatusCode HttpStatusCode.OK "Unexpected status code"
                    let result = r.Content.ReadAsStringAsync().Result
                    Expect.equal result expected "Unexpected result"
                else
                    Expect.equal r.StatusCode HttpStatusCode.NotFound "Unexpected status code"

            test "hello"  "sub/hello"
            test "friend" "sub/friend"
            test null  "subxy/hello"

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

        let encodedQueryNames (client: HttpClient) (server: TestServer) =
            let cases = [
                "a%20b=first&a+b=second&%61%20b=last", "a b", ["first"; "second"; "last"]
                "%61=one&a=two", "a", ["one"; "two"]
                "%C3%BC%E6%9D%B1%E4%BA%AC=value", "ü東京", ["value"]
                "items%5B%5D=value", "items[]", ["value"]
                "a%2Bb=one%2Btwo", "a+b", ["one+two"]
                "%25name=%2520", "%name", ["%20"]
                "%2520=literal", "%20", ["literal"]
                "%26%3D%3F%23=value", "&=?#", ["value"]
            ]

            for query, name, values in cases do
                let pathName = Uri.EscapeDataString name
                use lookup = client.GetAsync($"http://{server.Host}/query-param/{pathName}?{query}").Result
                Expect.equal lookup.StatusCode HttpStatusCode.OK $"Lookup status: {query}"
                Expect.equal (lookup.Content.ReadAsStringAsync().Result) values.Head $"Decoded lookup: {query}"

                use grouped = client.GetAsync($"http://{server.Host}/query-params?{query}").Result
                Expect.equal grouped.StatusCode HttpStatusCode.OK $"Grouping status: {query}"
                Expect.equal (grouped.Content.ReadAsStringAsync().Result) $"{name}:{values}" $"Decoded grouping: {query}"

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

        let bodyUtf8 (client: HttpClient) (server: TestServer) =
            for name,data,expected in BodyUtf8.cases do
                for path,wanted in ["body-utf8",Encoding.UTF8.GetBytes expected;"body",data] do
                    use content = new ByteArrayContent(data)
                    use response = client.PostAsync($"http://{server.Host}/{path}",content,server.CancellationToken).GetAwaiter().GetResult()
                    Expect.equal response.StatusCode HttpStatusCode.OK $"{name}: string/byte binding status"
                    // Read response bytes so HttpClient cannot silently strip a retained BOM.
                    Expect.sequenceEqual (response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()) wanted $"{name}: exact string/byte binding result"

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
                    let mutable i = 1

                    while i > 0 do
                        try
                            File.Delete absolutePath
                            i <- 0
                        with
                        | :? IOException when i < 3 ->
                            Log.warn $"Failed to delete file '{absolutePath}', retrying..."
                            Thread.Sleep 200
                            inc &i

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
            for dir in [""; "/custom-sub"] do
                use response = client.GetAsync($"http://{server.Host}{dir}/", server.CancellationToken).Result
                Expect.equal response.StatusCode HttpStatusCode.MovedPermanently "Index alias retains its permanent redirect"
                Expect.equal (response.Headers.GetValues("Location") |> Seq.toList)
                    [dir + CustomResources.indexPath] "Index redirect retains its normalized served path"
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
                "Encoded query names", Cases.encodedQueryNames
                "Header",             Cases.header
                "Method",             Cases.method
                "Method filters",     Cases.methodFilters
                "Path",               Cases.path
                "Body",               Cases.body
                "UTF-8 body binding", Cases.bodyUtf8
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

        testList "Core.HttpBackend" [
            BodyUtf8.tests
            createTests true
            createTests false
        ]