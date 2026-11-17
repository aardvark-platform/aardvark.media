namespace Aardvark.UI.Tests

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open Suave
open Suave.Operators
open Expecto

module ``Server Tests`` =

    let private timeout = TimeSpan.FromSeconds 5.0
    let private entries = [
        "start", (fun port token content -> Aardvark.UI.Suave.Server.start $"http://127.0.0.1:{port}" token content)
        "startLocalhost", Aardvark.UI.Suave.Server.startLocalhost
    ]
    let private freePort () = Aardvark.UI.Server.getFreeTcpPort IPAddress.Loopback
    let private drain (server : Task) =
        try server.WaitAsync(timeout).GetAwaiter().GetResult()
        with :? OperationCanceledException -> ()

    let private withStart (source : CancellationTokenSource) unblock start action =
        let result = TaskCompletionSource<Choice<Task,exn>>(TaskCreationOptions.RunContinuationsAsynchronously)
        let thread = Thread(ThreadStart(fun () ->
            try result.TrySetResult(Choice1Of2(start())) |> ignore
            with error -> result.TrySetResult(Choice2Of2 error) |> ignore))
        thread.IsBackground <- true
        thread.Start()
        try action result.Task
        finally
            source.Cancel()
            unblock()
            // Bound original-code hangs without canceling F#'s process-wide default token.
            // Observations are taken before this fallback, so interruption cannot pass a regression.
            if not (thread.Join 100) then
                try thread.Interrupt() with :? ThreadStateException -> ()
            Expect.isTrue (thread.Join timeout) "Startup thread must drain after owned cancellation or fallback interruption"
            match result.Task.GetAwaiter().GetResult() with
            | Choice1Of2 server -> drain server
            | Choice2Of2 _ -> ()

    let private canceled gated =
        let failures = ResizeArray<string>()
        for name, start in entries do
            use source = new CancellationTokenSource()
            use entered = new ManualResetEventSlim(false)
            use release = new ManualResetEventSlim(false)
            let mutable enumerations = 0
            let content = seq {
                Interlocked.Increment(&enumerations) |> ignore
                if gated then
                    entered.Set()
                    if not (release.Wait timeout) then failwith "Content enumeration gate timed out"
                yield Successful.OK "never served"
            }
            if not gated then source.Cancel()
            let port = freePort()
            let observed = withStart source release.Set (fun () -> start port source.Token content) (fun result ->
                if gated then
                    Expect.isTrue (entered.Wait timeout) $"{name}: startup reaches content enumeration"
                    Expect.isFalse result.IsCompleted "No readiness result while content enumeration is gated"
                    source.Cancel()
                    Expect.isFalse result.IsCompleted "Cancellation cannot bypass synchronous content enumeration"
                    release.Set()
                if result.Wait timeout then Some result.Result else None)
            match observed with
            | Some (Choice2Of2 (:? OperationCanceledException as error)) ->
                Expect.equal error.CancellationToken source.Token $"{name}: preserve supplied cancellation identity"
            | Some (Choice2Of2 error) -> failures.Add $"{name}: unexpected {error.GetType().Name}: {error.Message}"
            | Some (Choice1Of2 _) -> failures.Add $"{name}: canceled startup returned a server instead of throwing"
            | None -> failures.Add $"{name}: synchronous listening wait ignored the canceled lifetime token"
            Expect.equal enumerations 1 "Content enumeration order remains unchanged"
        Expect.isEmpty failures (String.concat "\n" failures)

    let private ready () =
        for name, start in entries do
            for nonempty in [false;true] do
                use source = new CancellationTokenSource()
                let port = freePort()
                let content =
                    if nonempty then [Filters.path "/first" >=> Successful.OK "first"; Filters.path "/second" >=> Successful.OK "second"]
                    else []
                withStart source ignore (fun () -> start port source.Token content) (fun result ->
                    Expect.isTrue (result.Wait timeout) $"{name}: synchronous startup completes"
                    let server =
                        match result.Result with
                        | Choice1Of2 server -> server
                        | Choice2Of2 error -> raise error
                    Expect.isFalse server.IsCompleted "Returned Task is server lifetime, not just readiness"
                    do
                        use connection = new TcpClient(AddressFamily.InterNetwork)
                        connection.ConnectAsync(IPAddress.Loopback,port).WaitAsync(timeout).GetAwaiter().GetResult()
                        Expect.equal (connection.Client.RemoteEndPoint :?> IPEndPoint).Address IPAddress.Loopback "Readiness means the loopback listener already accepts connections"
                    use client = new HttpClient(Timeout=timeout)
                    for path, expected in (if nonempty then ["/first","first";"/second","second"] else []) do
                        use request = new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}{path}")
                        request.Headers.Add("Origin","https://example.invalid")
                        use response = client.SendAsync(request).GetAwaiter().GetResult()
                        Expect.equal response.StatusCode HttpStatusCode.OK "Routing is ready immediately on return"
                        Expect.equal (response.Content.ReadAsStringAsync().GetAwaiter().GetResult()) expected "Distinct routes remain intact"
                        Expect.equal (response.Headers.GetValues("Access-Control-Allow-Origin") |> Seq.toList) ["https://example.invalid"] "CORS retains allowed origins"
                        Expect.equal (response.Headers.GetValues("Access-Control-Allow-Credentials") |> Seq.toList) ["true"] "CORS credentials remain unchanged"
                    Expect.isFalse server.IsCompleted "Serving requests does not complete the lifetime Task"
                    source.Cancel()
                    drain server
                    Expect.isTrue server.IsCompleted "Post-start lifetime cancellation stops the server")

    let private invalidUrls () =
        for cancel in [false;true] do
            for url, expectedType, expectedMessage in [
                "not a URL", typeof<FormatException>, "Invalid URL: not a URL"
                "https://127.0.0.1:12345", typeof<NotSupportedException>, "Protocol 'https' not supported."
                "http://example.invalid:12345", typeof<FormatException>, "Invalid IP address: example.invalid"
            ] do
                use source = new CancellationTokenSource()
                if cancel then source.Cancel()
                let mutable enumerated = false
                let content = seq { enumerated <- true; yield Successful.OK "unused" }
                let actual = withStart source ignore
                                 (fun () -> Aardvark.UI.Suave.Server.start url source.Token content)
                                 (fun result ->
                                     Expect.isTrue (result.Wait timeout) "URL validation must finish synchronously"
                                     result.Result)
                match actual with
                | Choice2Of2 error ->
                    Expect.equal (error.GetType()) expectedType "Synchronous URL failure classification"
                    Expect.equal error.Message expectedMessage "Synchronous URL failure message"
                | Choice1Of2 _ -> failtest "Invalid URL unexpectedly started a server"
                Expect.isFalse enumerated "URL errors still precede content enumeration and cancellation"

    [<Tests>]
    let tests = testSequenced <| testList "Core.Server.Suave" [
        test "Already-canceled lifetimes cancel the synchronous listening wait" { canceled false }
        test "Cancellation during gated content enumeration cancels before listening" { canceled true }
        test "Readiness routing CORS and post-start cancellation preserve server lifetime" { ready() }
        test "Invalid URL protocol and address failures remain synchronous" { invalidUrls() }
    ]
