namespace Aardvark.UI.Tests

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto
open ``HttpBackend Tests``

module ``MutableApp Title Tests`` =

    type private TitleCase =
        { Name : string; Input : string option; Literal : string; Encoded : string }

    let private title name input encoded =
        { Name = name; Input = Some input; Literal = (if isNull input then "" else input); Encoded = encoded }

    let private cases = [|
        { Name = "default"; Input = None; Literal = @"Aardvark rocks \o/"; Encoded = @"Aardvark rocks \o/" }
        title "named entities" "Fish &amp; Chips" "Fish &amp;amp; Chips"
        title "numeric entities" "Code &#65; and &#x41;" "Code &amp;#65; and &amp;#x41;"
        title "nested entities" "Nested &amp;amp; &#38;lt;" "Nested &amp;amp;amp; &amp;#38;lt;"
        title "unterminated entities" "Legacy &copy test &amp tail" "Legacy &amp;copy test &amp;amp tail"
        title "encoded angles" "Angles &lt;tag&gt;" "Angles &amp;lt;tag&amp;gt;"
        title "script closing title" "before</title><script>window.titleInjected=1</script><title>after"
            "before&lt;/title&gt;&lt;script&gt;window.titleInjected=1&lt;/script&gt;&lt;title&gt;after"
        title "mixed-case closing title" "</TiTlE><script>window.titleInjected=2</script><title>"
            "&lt;/TiTlE&gt;&lt;script&gt;window.titleInjected=2&lt;/script&gt;&lt;title&gt;"
        title "element closing title" "before</title><div id=title-injected>payload</div><title>after"
            "before&lt;/title&gt;&lt;div id=title-injected&gt;payload&lt;/div&gt;&lt;title&gt;after"
        title "ordinary" "Ordinary title" "Ordinary title"
        title "unicode" "München 東京 🦊" "M&#252;nchen 東京 &#129418;"
        title "quotes" "\"quoted\" 'title'" "&quot;quoted&quot; &#39;title&#39;"
        title "raw ampersand" "A & B" "A &amp; B"
        title "raw angles" "<tag> in title" "&lt;tag&gt; in title"
        title "path" @"path\name / value" @"path\name / value"
        title "empty" "" ""
        title "null" null ""
    |]

    let private resource (assembly : Reflection.Assembly) suffix =
        let name = assembly.GetManifestResourceNames() |> Array.filter (fun n -> n.EndsWith(suffix, StringComparison.Ordinal)) |> Array.exactlyOne
        use input = assembly.GetManifestResourceStream name
        use reader = new StreamReader(input)
        reader.ReadToEnd()

    let private template () = resource typeof<DomNode<unit>>.Assembly "template.html"

    let private createApp () =
        let definition : App<unit, unit, unit> =
            { initial = ()
              unpersist = { create = id; update = fun _ _ -> () }
              update = fun _ _ -> ()
              threads = fun _ -> ThreadPool.empty
              view = fun _ -> div [] [text "DOM-only title fixture"] }
        App.start definition

    let private content (http : IHttpBackend<'Context, 'Handler>) (app : MutableApp<unit, unit, unit>) =
        let defaultTitle = app.DocumentTitle
        Expect.equal defaultTitle @"Aardvark rocks \o/" "Unchanged default title"
        let handlers =
            cases |> Array.map (fun case ->
                app.DocumentTitle <- case.Input |> Option.defaultValue defaultTitle
                // Root HTML requests never use a rendering runtime, render nodes or GPU compression.
                let handler = MutableApp.toWebPart http Unchecked.defaultof<IRuntime> app
                Expect.equal app.DocumentTitle (case.Input |> Option.defaultValue defaultTitle) "Do not encode the stored property"
                handler)
        http.request (fun request -> handlers.[Int32.Parse(request.QueryParam "case" |> Option.defaultValue "0")])

    let private withResponses useGiraffe action =
        use app = createApp ()
        let server : TestServer =
            if useGiraffe then GiraffeTestServer(fun _ -> [content Giraffe.HttpBackend.Instance app])
            else SuaveTestServer(fun _ -> [content Suave.HttpBackend.Instance app])
        use lifetime = server.Acquire()
        use client = new HttpClient(Timeout = TimeSpan.FromSeconds 5.0)
        let responses =
            cases |> Array.mapi (fun i case ->
                use response = client.GetAsync($"http://{server.Host}/?case={i}").GetAwaiter().GetResult()
                Expect.equal response.StatusCode HttpStatusCode.OK "Actual root route must succeed"
                Expect.equal response.Content.Headers.ContentType.MediaType "text/html" "Root content type"
                case, response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
        action responses

    let private checkHtml responses =
        let parts = (template ()).Split([|"__TITLE__"|], StringSplitOptions.None)
        Expect.equal parts.Length 2 "Exactly one title substitution point"
        for case, html in responses do
            Expect.equal html (parts.[0] + case.Encoded + parts.[1]) $"{case.Name}: encode once without changing surrounding HTML"

    let private mouse =
        match onMouseMoveAbs (fun position size -> position, size) with
        | _, AttributeValue.Event handler -> handler
        | _ -> failwith "Expected generated mouse event"

    let private dynamicTitle = "Updated &amp; <b>literal</b> 東京"

    let private dynamicBootAndData () =
        let node = div [] [] |> Aardvark.UI.Primitives.Html.title false (AVal.constant dynamicTitle)
        use reader = node.Channels.["chDocumentTitle"].GetReader()
        let data = reader.GetMessages AdaptiveToken.Top
        match node.Boot with
        | ValueSome boot -> boot "dynamic-title", data
        | _ -> failwith "Expected dynamic title boot code"

    let private browser (responses : (TitleCase * string)[]) =
        let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then
            skiptest "Set AARDVARK_CHROMIUM to an explicit Chromium executable (required in Linux CI)."
        Expect.isTrue (File.Exists executable) "AARDVARK_CHROMIUM must name an executable"
        let directory = Path.Combine(Path.GetTempPath(), "aardvark-titles-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(directory, "resources")) |> ignore
        try
            let assembly = typeof<DomNode<unit>>.Assembly
            for name in ["jquery-4.0.0.min.js"; "rgbcolor.js"; "aardvark.js"; "renderer.js"; "aardvark.css"] do
                File.WriteAllText(Path.Combine(directory, "resources", name), resource assembly name)
            File.WriteAllText(Path.Combine(directory, "title-browser.js"), resource typeof<TitleCase>.Assembly "title-browser.js")
            let boot, batch = dynamicBootAndData ()
            let code = Event.toString "title-host" "onmousemove" mouse |> WebUtility.HtmlEncode
            let control =
                $"<div id='title-host' style='position:absolute;left:40px;top:50px;width:200px;height:120px' onmousemove=\"if(this.id!=='title-host'||event.currentTarget!==this||event.target.id!=='title-child')throw new Error('Wrong inline receiver');trace.push('receiver:host');trace.push('before');{code};trace.push('after');\">" +
                "<div id='title-child' style='position:absolute;left:50px;top:20px;width:40px;height:30px'>child</div></div><div id='dynamic-title'></div>"
            let bootstrap = "<script>window.scriptErrors=[];window.titleInjected=0;window.trace=[];window.packets=[];window.connections=0;" +
                            "addEventListener('error',e=>scriptErrors.push(e.message||'resource error'),true);" +
                            "addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));" +
                            "window.WebSocket=class{constructor(){connections++;window.titleSocket=this;this.readyState=3;}send(text){if(text!=='#ping'){packets.push(JSON.parse(text));trace.push('send:host');}}};</script>"
            let fixtures =
                responses |> Array.mapi (fun i (case, html) ->
                    let config = JsonSerializer.Serialize {| expected = case.Literal; dynamicExpected = dynamicTitle; dynamicBoot = boot; dynamicBatch = batch |}
                    // Only test instrumentation changes: the actual response's title and resource references remain intact.
                    let page = html.Replace("<head>", "<head>" + bootstrap).Replace("</body>", control + "<script>window.titleFixture=" + config + ";</script><script src='title-browser.js'></script></body>")
                    let filename = $"case-{i}.html"
                    File.WriteAllText(Path.Combine(directory, filename), page)
                    {| name = case.Name; url = filename |})
            let driver = "<html><head><meta charset='utf-8'></head><body><script>window.titleCases=" + JsonSerializer.Serialize fixtures + ";</script><script src='title-browser.js'></script></body></html>"
            let path = Path.Combine(directory, "index.html")
            File.WriteAllText(path, driver)
            let start = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            for argument in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage";
                             "--allow-file-access-from-files"; "--no-first-run"; "--disable-background-networking";
                             "--virtual-time-budget=20000"; "--user-data-dir=" + Path.Combine(directory, "profile"); "--dump-dom"; Uri(path).AbsoluteUri] do
                start.ArgumentList.Add argument
            use chromium = Process.Start start
            let output, errors = chromium.StandardOutput.ReadToEndAsync(), chromium.StandardError.ReadToEndAsync()
            if not (chromium.WaitForExit 60000) then
                chromium.Kill(true)
                chromium.WaitForExit()
                failtest "Chromium timed out"
            Expect.equal chromium.ExitCode 0 $"Chromium failed: {errors.Result}"
            let result = Regex.Match(output.Result, "<pre id=\"title-results\">([^<]+)</pre>")
            Expect.isTrue result.Success $"Missing completed browser results: {output.Result}\n{errors.Result}"
            use document = JsonDocument.Parse(WebUtility.HtmlDecode result.Groups.[1].Value)
            let root = document.RootElement
            Expect.equal (root.GetProperty("completed").GetInt32()) cases.Length "Every isolated response must finish"
            let failures = root.GetProperty("failures").EnumerateArray() |> Seq.map string |> Seq.toList
            Expect.isEmpty failures (String.concat "\n" failures)
            let packets = root.GetProperty("packets").EnumerateArray() |> Seq.toArray
            Expect.equal packets.Length cases.Length "One actual generated mouse packet per response"
            for packet in packets do
                Expect.equal (packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString())
                    ("title-host", "onmousemove") "Generated handler destination"
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map (fun v -> v.GetString()) |> Seq.toList
                Expect.equal (mouse.serverSide Guid.Empty "title-host" args |> Seq.toList)
                    [V2d(71.0, 33.0), V2d(200.0, 120.0)] "Independent host-relative geometry through the actual server parser"
        finally
            Directory.Delete(directory, true)

    [<Tests>]
    let tests =
        testList "MutableApp.Title" [
            test "Giraffe root encodes title text once and preserves the template" { withResponses true checkHtml }
            test "Suave root encodes title text once and preserves the template" { withResponses false checkHtml }
            test "Rendered root titles and generated bubbling controls" { withResponses true browser }
        ]
