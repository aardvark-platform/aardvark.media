namespace Aardvark.UI.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Aardvark.UI
open Expecto

module ``SendFile Tests`` =

    let private http = Giraffe.HttpBackend.Instance
    let private bound = 65536
    let private bytes count = Array.init count (fun i -> byte ((i * 37 + i / 251) % 256))

    let private withFile (data: byte[]) action =
        let root = Path.Combine(Path.GetTempPath(), $"aardvark-send-file-{Guid.NewGuid():N}")
        Directory.CreateDirectory root |> ignore
        let path = Path.Combine(root, "binary 東京 file.bin")
        try
            File.WriteAllBytes(path, data)
            action root path
        finally
            Directory.Delete(root, true)

    let private services root =
        let environment =
            { new IWebHostEnvironment with
                member _.ApplicationName with get() = "SendFile.Tests" and set _ = ()
                member _.EnvironmentName with get() = "Testing" and set _ = ()
                member _.ContentRootPath with get() = root and set _ = ()
                member _.ContentRootFileProvider with get() = new NullFileProvider() :> IFileProvider and set _ = ()
                member _.WebRootPath with get() = root and set _ = ()
                member _.WebRootFileProvider with get() = new NullFileProvider() :> IFileProvider and set _ = () }
        ServiceCollection().AddSingleton<IWebHostEnvironment>(environment).BuildServiceProvider()

    // Inspect bytes without buffering the response; both Stream async overloads are exercised.
    type private Sink(expected: byte[], maximum: int, onWrite: int -> CancellationToken -> Task) =
        inherit Stream()
        let mutable count = 0
        let mutable writes = 0
        let mutable closed = false
        let write (data: ReadOnlyMemory<byte>) token =
            Expect.isLessThanOrEqual data.Length maximum "Response writes must be bounded, not payload-sized"
            Expect.isLessThanOrEqual (count + data.Length) expected.Length "No bytes beyond the captured file length"
            Expect.isTrue (data.Span.SequenceEqual(expected.AsSpan(count, data.Length))) "Exact binary bytes, including buffer boundaries"
            writes <- writes + 1
            task {
                do! onWrite writes token
                Expect.isTrue (data.Span.SequenceEqual(expected.AsSpan(count, data.Length))) "Buffer remains valid until the async response write completes"
                count <- count + data.Length
            }
        member _.Count = count
        member _.Writes = writes
        member _.Closed = closed
        override _.CanRead = false
        override _.CanSeek = false
        override _.CanWrite = true
        override _.Length = int64 count
        override _.Position with get() = int64 count and set _ = raise (NotSupportedException())
        override _.Flush() = ()
        override _.Read(_, _, _) = raise (NotSupportedException())
        override _.Seek(_, _) = raise (NotSupportedException())
        override _.SetLength _ = raise (NotSupportedException())
        override _.Write(_, _, _) = failwith "File response writes must be asynchronous"
        override _.WriteAsync(data: byte[], offset, length, token) = write (ReadOnlyMemory(data, offset, length)) token :> Task
        override _.WriteAsync(data: ReadOnlyMemory<byte>, token) = ValueTask(write data token :> Task)
        override _.Dispose disposing =
            closed <- true
            base.Dispose disposing

    let private asynchronous _ (_: CancellationToken) = task { do! Task.Yield() } :> Task

    let private context provider method status body =
        let ctx = DefaultHttpContext()
        ctx.RequestServices <- provider
        ctx.Request.Method <- method
        ctx.Response.StatusCode <- status
        ctx.Response.ContentType <- "application/x-file-test"
        ctx.Response.Headers.["X-File-Test"] <- "preserved"
        ctx.Response.Body <- body
        ctx

    let private invoke path (ctx: HttpContext) =
        let next _ = failwith "sendFile must terminate the handler chain"
        (http.sendFile path next ctx).WaitAsync(TimeSpan.FromSeconds 15.0).GetAwaiter().GetResult()

    let private released path =
        use file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        Expect.isTrue file.CanRead "The transfer released its file handle"

    [<Tests>]
    let tests =
        testList "Core.HttpBackend.Giraffe.FileStreaming" [
            test "bounded writes preserve binary BOM empty and multibuffer files" {
                let payloads = [
                    yield bytes (6 * 1024 * 1024 + 131)
                    yield bytes (8 * 1024 * 1024 + 65537)
                    yield Array.empty
                    yield Array.append (Encoding.UTF8.GetPreamble()) (Encoding.UTF8.GetBytes "Hello π 東京\000")
                    yield Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "Hello π 東京\000")
                    for count in [256; bound - 1; bound; bound + 1; 3 * bound + 17] do yield bytes count
                ]
                for data in payloads do
                    withFile data (fun root path ->
                        use provider = services root
                        for input in [path; Path.GetFileName path] do
                            use sink = new Sink(data, bound, asynchronous)
                            let ctx = context provider "GET" 202 sink
                            let result = invoke input ctx
                            Expect.isTrue (obj.ReferenceEquals(result.Value, ctx)) "Returns the same handled context"
                            Expect.equal sink.Count data.Length "All announced bytes delivered"
                            Expect.equal ctx.Response.ContentLength (Nullable(int64 data.Length)) "Length from the file"
                            Expect.isFalse sink.Closed "The caller owns its response stream"
                            released path)
            }

            test "native metadata rules retain special status CONNECT and HEAD behavior" {
                withFile (bytes 256) (fun root path ->
                    use provider = services root
                    for method in ["GET"; "POST"; "CONNECT"; "connect"; "HEAD"; "head"] do
                        for status in [101; 199; 200; 202; 204; 205; 206; 304; 404] do
                            for existing in [Nullable(); Nullable 12345L] do
                                use sink = new Sink(bytes 256, bound, asynchronous)
                                let ctx = context provider method status sink
                                ctx.Response.ContentLength <- existing
                                let result = invoke (Path.GetFileName path) ctx
                                let head = method = "HEAD" || method = "head"
                                let expected =
                                    if (status >= 100 && status < 200) || status = 204 || (not head && method = "CONNECT" && status >= 200 && status < 300) then existing
                                    elif status = 205 then Nullable 0L
                                    else Nullable 256L
                                Expect.equal ctx.Response.ContentLength expected $"{method}/{status}: Content-Length"
                                Expect.equal ctx.Response.StatusCode status "Supplied status remains unchanged"
                                Expect.equal ctx.Response.ContentType "application/x-file-test" "Supplied content type"
                                Expect.equal (string ctx.Response.Headers.["X-File-Test"]) "preserved" "Supplied header"
                                Expect.equal sink.Count (if head then 0 else 256) "Retain body/HEAD behavior even when the native server would reject a body for this status"
                                Expect.isTrue (obj.ReferenceEquals(result.Value, ctx)) "Handler termination"
                                released path)
            }

            test "real HTTP GET and HEAD retain paths bytes headers and handler termination" {
                let (>=>) a b = http.compose a b
                let handler =
                    http.request (fun request ->
                        http.status 202 >=> http.mimeType "application/octet-stream"
                        >=> http.header "X-File-Test" "preserved"
                        >=> http.sendFile (request.QueryParam "path" |> Option.get))
                let server = GiraffeTestServer(fun _ -> [http.route "/file" >=> handler; http.notFound "Not handled"])
                use _server = server.Acquire()
                use client = new HttpClient(Timeout = TimeSpan.FromSeconds 15.0)
                for data in [Array.empty; bytes 256; Array.append (Encoding.UTF8.GetPreamble()) (Encoding.UTF8.GetBytes "Hello π!"); bytes (3 * bound + 17)] do
                    withFile data (fun _ path ->
                        for input in [path; Path.GetRelativePath(Directory.GetCurrentDirectory(), path)] do
                            for method in [System.Net.Http.HttpMethod.Get; System.Net.Http.HttpMethod.Head] do
                                use request = new HttpRequestMessage(method, $"http://{server.Host}/file?path={Uri.EscapeDataString input}")
                                use response = client.SendAsync(request).GetAwaiter().GetResult()
                                Expect.equal response.StatusCode HttpStatusCode.Accepted "Supplied status"
                                Expect.equal response.Content.Headers.ContentType.MediaType "application/octet-stream" "Content type"
                                Expect.equal (response.Headers.GetValues("X-File-Test") |> Seq.toList) ["preserved"] "Custom header"
                                Expect.equal response.Content.Headers.ContentLength (Nullable(int64 data.Length)) "Announced file length"
                                let actual = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                                Expect.sequenceEqual actual (if method = System.Net.Http.HttpMethod.Head then Array.empty else data) "Exact response bytes"
                                released path)
            }

            test "write failures cancellation and missing paths release transfer resources" {
                withFile (bytes (3 * bound + 17)) (fun root path ->
                    use provider = services root
                    for asynchronousFailure in [false; true] do
                        let error = IOException "response write failed"
                        let fail _ _ =
                            if asynchronousFailure then
                                task {
                                    do! Task.Yield()
                                    return raise error
                                } :> Task
                            else raise error
                        use sink = new Sink(bytes (3 * bound + 17), Int32.MaxValue, fail)
                        let ctx = context provider "GET" 202 sink
                        let actual = try invoke path ctx |> ignore; None with e -> Some e
                        Expect.isTrue (obj.ReferenceEquals(actual.Value, error)) "Preserve the exact response-write exception"
                        Expect.isFalse sink.Closed "Failure does not dispose the caller's response stream"
                        released path
                    for before in [true; false] do
                        use cancellation = new CancellationTokenSource()
                        let cancel _ token =
                            Expect.equal token cancellation.Token "Pass RequestAborted to asynchronous response writes"
                            cancellation.Cancel()
                            Task.FromCanceled(cancellation.Token)
                        use sink = new Sink(bytes (3 * bound + 17), Int32.MaxValue, cancel)
                        let ctx = context provider "GET" 202 sink
                        ctx.RequestAborted <- cancellation.Token
                        if before then cancellation.Cancel()
                        let actual = try invoke path ctx |> ignore; None with e -> Some e
                        Expect.isTrue (actual.Value :? OperationCanceledException) "Aborted transfer is canceled"
                        Expect.equal ((actual.Value :?> OperationCanceledException).CancellationToken) cancellation.Token "Cancellation token"
                        if before then Expect.equal sink.Writes 0 "Pre-aborted transfers never write a response body"
                        Expect.isFalse sink.Closed "Canceled transfer retains caller-owned response stream"
                        released path
                    for method in ["GET"; "HEAD"] do
                        use sink = new Sink(Array.empty, bound, asynchronous)
                        for missing in [path + ".missing"; Path.Combine(root, "missing", "file.bin")] do
                            let ctx = context provider method 202 sink
                            Expect.throws (fun () -> invoke missing ctx |> ignore) "Missing file/path remains an error"
                        let ctx = context provider method 202 sink
                        Expect.throws (fun () -> invoke root ctx |> ignore) "Directory is not a file"
                    released path)
            }

            test "growth is capped and early EOF fails rather than completing a short response" {
                if OperatingSystem.IsWindows() then Tests.skiptest "Windows read sharing prevents in-place file mutation during the transfer"
                for grow in [true; false] do
                    let data = bytes (3 * bound + 17)
                    withFile data (fun root path ->
                        use provider = services root
                        let mutate index _ =
                            task {
                                do! Task.Yield()
                                if index = 1 then
                                    use file = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)
                                    if grow then
                                        file.Position <- file.Length
                                        file.Write(bytes (bound + 37))
                                    else file.SetLength(int64 bound)
                            } :> Task
                        use sink = new Sink(data, Int32.MaxValue, mutate)
                        let ctx = context provider "GET" 202 sink
                        if grow then
                            invoke path ctx |> ignore
                            Expect.equal sink.Count data.Length "Appended bytes must not exceed the announced snapshot length"
                        else
                            let actual = try invoke path ctx |> ignore; None with e -> Some e
                            match actual with
                            | None -> Expect.equal sink.Count data.Length "Success requires the complete announced body, never a short response"
                            | Some (:? EndOfStreamException) -> Expect.isLessThan sink.Count data.Length "Early EOF reports the incomplete transfer"
                            | Some error -> raise error
                        Expect.equal ctx.Response.ContentLength (Nullable(int64 data.Length)) "Use the original open handle's captured length"
                        released path)
            }
        ]
