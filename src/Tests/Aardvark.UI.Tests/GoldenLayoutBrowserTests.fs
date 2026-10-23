namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Net
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives.Golden
open FSharp.Data.Adaptive
open Expecto
open ``HttpBackend Tests``

module ``GoldenLayout Browser Tests`` =

    // Literal page identities, never derived by decoding the proposed URL rewrite.
    let private ids = [|
        "plain-panel"; "panel&injected=yes"; "panel+plus"; "panel#fragment"
        "literal%26escape"; "literal%23escape"; "literal%2F%3F%3D"
        "line\nbreak\rreturn\r\nend"; "with space"; " padded "
        "東京 ü🦊"; "quotes\"'`"; "path/part?key=value"; "equals=question?slash/"
    |]

    let private paths = [|"/"; "/apps/nested/"|]
    let private json value = JsonSerializer.Serialize value

    let private script () =
        let assembly = Reflection.Assembly.GetExecutingAssembly()
        let name = assembly.GetManifestResourceNames() |> Array.filter (_.EndsWith("golden-page-browser.js", StringComparison.Ordinal)) |> Array.exactlyOne
        use input = assembly.GetManifestResourceStream name
        use reader = new StreamReader(input)
        reader.ReadToEnd()

    let private code self = function
        | ValueSome generate -> generate self
        | _ -> failwith "Expected generated GoldenLayout lifecycle code"

    let private fixtures () =
        [| for keepAlive in [true; false] do
               for index, pageId in ids |> Array.indexed do
                   let element : Element =
                       { Id = pageId; Title = "Panel"; Closable = true; Header = Some Header.Top
                         Buttons = None; MinSize = None; Size = Size.Weight 1; KeepAlive = keepAlive }
                   let model = GoldenLayout.create LayoutConfig.Default (Layout.Element element)
                   let adaptive = AdaptiveGoldenLayout model
                   let node : DomNode<unit> = GoldenLayout.view [] adaptive
                   let self = $"layout-{keepAlive}-{index}"
                   use reader = node.Channels.["channelSet"].GetReader()
                   Expect.isEmpty (reader.GetMessages AdaptiveToken.Top) "No initial set-layout command"
                   let empty = GoldenLayout.update (GoldenLayout.Message.SetWindowLayout { Root = None; PopoutWindows = [] }) model
                   transact (fun () -> adaptive.Update empty)
                   let emptyBatch = reader.GetMessages AdaptiveToken.Top
                   let restored = GoldenLayout.update (GoldenLayout.Message.SetWindowLayout model.DefaultLayout) empty
                   transact (fun () -> adaptive.Update restored)
                   let restoreBatch = reader.GetMessages AdaptiveToken.Top
                   Expect.equal (emptyBatch.Length, restoreBatch.Length) (1, 1) "Actual serialized layout channel messages"
                   yield {| id = pageId; self = self; keepAlive = keepAlive
                            boot = code self node.Boot; shutdown = code self node.Shutdown
                            emptyBatch = emptyBatch; restoreBatch = restoreBatch |}, node.Required |]

    let private mouse =
        match onMouseMoveAbs (fun position size -> position, size) with
        | _, AttributeValue.Event handler -> handler
        | _ -> failwith "Expected generated mouse event"

    let private hostHtml path fixtures references =
        let referenceHtml (reference : Reference) =
            let url = WebUtility.HtmlEncode reference.url
            match reference.kind with
            | Script -> $"<script src='{url}'></script>"
            | Stylesheet -> $"<link rel='stylesheet' href='{url}'>"
            | _ -> failwith "Unexpected GoldenLayout dependency"
        let dependencies = references |> List.map referenceHtml |> String.concat ""
        let mouseCode = Event.toString "receiver" "onmousemove" mouse |> WebUtility.HtmlEncode
        "<!doctype html><html><head><meta charset='utf-8'><title>Golden page fixture</title>" +
        "<script>window.scriptErrors=[];window.trace=[];window.packets=[];window.connections=0;" +
        "addEventListener('error',e=>scriptErrors.push(e.message||'resource error'),true);" +
        "addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));" +
        "window.WebSocket=class{constructor(){connections++;window.fixtureSocket=this;this.readyState=3;}send(text){if(text!=='#ping'){const p=JSON.parse(text);packets.push(p);if(p.name==='onmousemove')trace.push('send:receiver');}}};</script>" +
        "<script src='resources/jquery-4.0.0.min.js'></script><script src='resources/aardvark.js'></script>" + dependencies +
        "</head><body><div id='layouts'></div>" +
        $"<div id='receiver' style='position:absolute;left:40px;top:50px;width:200px;height:120px' onmousemove=\"if(this.id!=='receiver'||event.currentTarget!==this||event.target.id!=='child')throw new Error('Wrong inline receiver');trace.push('receiver:parent');trace.push('before');{mouseCode};trace.push('after');\">" +
        "<div id='child' style='position:absolute;left:50px;top:20px;width:40px;height:30px'>child</div></div>" +
        "<script>window.goldenFixtures=" + json fixtures + ";window.expectedPath=" + json path + ";</script>" +
        "<script src='/golden-page-browser.js'></script></body></html>"

    type private Request = { Path : string; Page : string; Query : (string * string list) list }

    let private runBrowser () =
        let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then skiptest "Set AARDVARK_CHROMIUM to an explicit Chromium executable; Ubuntu CI requires it."
        Expect.isTrue (File.Exists executable) "AARDVARK_CHROMIUM must name an executable"
        let fixtures, references = fixtures () |> Array.unzip
        let responses = paths |> Array.map (fun path -> path, hostHtml path fixtures references.[0]) |> Map.ofArray
        let requests = ConcurrentQueue<Request>()
        let http = Giraffe.HttpBackend.Instance
        let resources = http.choose [http.assembly typeof<Pages>.Assembly; http.assembly typeof<LayoutConfig>.Assembly]
        let content = http.request (fun request ->
            match request.Path with
            | "/runner" ->
                http.html ("<!doctype html><html><head><title>Golden driver</title></head><body><script>window.goldenPaths=" + json paths + ";</script><script src='/golden-page-browser.js'></script></body></html>")
            | "/golden-page-browser.js" -> http.compose (http.mimeType "text/javascript") (http.text (script ()))
            | path when responses.ContainsKey path ->
                // Execute the real pages selector against the actual HTTP request.
                let mutable selected = Pages.Body
                let selector = pages (fun page -> selected <- page; DomNode.Empty<unit>()) :?> PageNode<unit>
                selector.Content request |> ignore
                match selected with
                | Pages.Body -> http.html responses.[path]
                | Pages.Page pageId ->
                    requests.Enqueue { Path = path; Page = pageId; Query = request.QueryParams |> Map.toList }
                    let query = request.QueryParams |> Map.toArray |> Array.map (fun (name, values) -> {| name = name; values = values |})
                    let result = {| page = pageId; path = path; query = query
                                    known = Array.contains pageId ids; token = Guid.NewGuid().ToString("N") |}
                    http.compose (http.header "Cache-Control" "no-store") (http.html ("<!doctype html><html><head><meta charset='utf-8'></head><body><script>window.requestedPage=" + json result + ";document.body.textContent=requestedPage.page;</script></body></html>"))
            | _ -> http.choose [resources; http.subRoute "/apps/nested" resources])
        let server = GiraffeTestServer(fun _ -> [content])
        use lifetime = server.Acquire()
        let directory = Path.Combine(Path.GetTempPath(), "aardvark-golden-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory directory |> ignore
        try
            let start = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            for argument in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage";
                             "--no-first-run"; "--disable-background-networking"; "--virtual-time-budget=45000";
                             "--user-data-dir=" + directory; "--dump-dom"; $"http://{server.Host}/runner"] do
                start.ArgumentList.Add argument
            use chromium = Process.Start start
            let output, errors = chromium.StandardOutput.ReadToEndAsync(), chromium.StandardError.ReadToEndAsync()
            if not (chromium.WaitForExit 90000) then
                chromium.Kill(true)
                chromium.WaitForExit()
                failtest "Chromium timed out"
            Expect.equal chromium.ExitCode 0 $"Chromium failed: {errors.Result}"
            let result = Regex.Match(output.Result, "<pre id=\"golden-results\">([^<]+)</pre>")
            Expect.isTrue result.Success $"Missing completed browser results: {output.Result}\n{errors.Result}"
            use document = JsonDocument.Parse(WebUtility.HtmlDecode result.Groups.[1].Value)
            let root = document.RootElement
            Expect.equal (root.GetProperty("completed").GetInt32()) (fixtures.Length * paths.Length) "All layout cases must finish"
            let failures = root.GetProperty("failures").EnumerateArray() |> Seq.map string |> Seq.toList
            Expect.isEmpty failures (String.concat "\n" failures)
            let packets = root.GetProperty("packets").EnumerateArray() |> Seq.toArray
            Expect.equal packets.Length paths.Length "One generated mouse packet per application path"
            for packet in packets do
                Expect.equal (packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString())
                    ("receiver", "onmousemove") "Generated bubbling handler destination"
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map (_.GetString()) |> Seq.toList
                Expect.equal (mouse.serverSide Guid.Empty "receiver" args |> Seq.toList)
                    [V2d(71.0, 33.0), V2d(200.0, 120.0)] "Actual server parser must receive host-relative coordinates and size"
            let requested = requests.ToArray()
            Expect.equal requested.Length (ids.Length * paths.Length * 3) "keepAlive navigates once; disposable frames navigate twice"
            for item in requested do
                Expect.contains paths item.Path "Unchanged requested application path"
                Expect.contains ids item.Page "Actual request must select an independently expected page"
                Expect.equal item.Query ["page", [item.Page]] "Exactly one page parameter, with no injected parameters"
            for path in paths do
                for pageId in ids do
                    Expect.equal (requested |> Array.filter (fun item -> item.Path = path && item.Page = pageId) |> Array.length) 3
                        $"{path}, {pageId}: exact actual page request count"
        finally
            Directory.Delete(directory, true)

    [<Tests>]
    let tests =
        testList "GoldenLayout.Browser" [
            test "Special-character pages, iframe ownership and bubbling" { runBrowser () }
        ]
