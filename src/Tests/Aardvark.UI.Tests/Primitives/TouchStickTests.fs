namespace Aardvark.UI.Tests

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open Expecto

module ``TouchStick Tests`` =

    type private Message = Start of string * V2d | Move of string * TouchStick.TouchStickState | Stop of string
    let private cases = [
        "escaped", ["apostrophe", ["owner's"]; "backslashes", [@"path\n\q"]; "trailing", ["tail\\"]
                    "cr", ["line\rbreak"]; "lf", ["line\nbreak"]; "crlf", ["line\r\nbreak"]]
        "controls", ["ordinary", ["move"]; "double-quote", ["double\"quote"]; "unicode", ["München 東京 🦊"]
                     "separators", ["line\u2028paragraph\u2029end"]; "empty-name", [""]]
        "multiple", ["multiple-ordinary", ["left"; "right"]; "multiple-literal", [@"left\literal"; "right's 名\n"]
                     "empty-config", []]
    ]
    let private configs (names : string list) : TouchStick.StickConfig list =
        names |> List.mapi (fun index name ->
            { name = name; radius = if index = 0 then 50.0 else 100.0
              area = if names.Length = 1 then Box2d(V2d(-1.0,-1.0), V2d(1.0,1.0))
                     elif index = 0 then Box2d(V2d(-1.0,-1.0), V2d(0.0,1.0))
                     else Box2d(V2d(0.0,-1.0), V2d(1.0,1.0)) })
    let private boot id names =
        let node : DomNode<Message> = div [] [] |> TouchStick.withTouchSticks (configs names)
        Expect.equal (node.Required |> List.map (fun r -> r.name, r.url, r.kind))
            ["touchstick.js", "resources/touchstick.js", Script] "Retain the declared dependency"
        match node.Boot with ValueSome boot -> boot id | _ -> failwith "Missing TouchStick Boot"

    let private resource (assembly : Assembly) file =
        let name = assembly.GetManifestResourceNames() |> Array.find (fun n ->
            n.Replace('\\','/').EndsWith("/" + file, StringComparison.Ordinal) || n.EndsWith("." + file, StringComparison.Ordinal))
        use stream = assembly.GetManifestResourceStream name
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let private html rows =
        let hosts = rows |> List.map (fun (id, names) ->
            "<div id=\"" + id + "\" style=\"position:fixed;left:40px;top:50px;width:200px;height:120px\">" +
            "<span style=\"position:absolute;left:50px;top:20px;width:40px;height:30px\"></span></div>" +
            "<script>prepare(" + JsonSerializer.Serialize id + "," + JsonSerializer.Serialize names + ");</script>" +
            "<script>" + boot id names + "</script><script>finish();</script>")
        "<!doctype html><meta charset=\"utf-8\"><script src=\"aardvark.js\"></script>" +
        "<script src=\"touchstick.js\"></script><script src=\"touchstick-names-browser.js\"></script><body>" +
        String.concat "" hosts + "<script>run();</script></body>"

    // Set AARDVARK_CHROMIUM to an installed Chromium; other runs explicitly skip.
    let private render executable rows =
        let folder = Path.Combine(Path.GetTempPath(), "aardvark-touchstick-names-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory folder |> ignore
        use browser = new Process()
        try
            File.WriteAllText(Path.Combine(folder,"aardvark.js"), resource typeof<DomNode<unit>>.Assembly "aardvark.js")
            File.WriteAllText(Path.Combine(folder,"touchstick.js"), resource typeof<TouchStick.StickConfig>.Assembly "touchstick.js")
            File.WriteAllText(Path.Combine(folder,"touchstick-names-browser.js"), resource typeof<Message>.Assembly "touchstick-names-browser.js")
            File.WriteAllText(Path.Combine(folder,"index.html"), html rows)
            browser.StartInfo <- ProcessStartInfo(executable, UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true)
            for arg in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage"; "--no-first-run"
                        "--no-default-browser-check"; "--disable-extensions"; "--disable-sync"; "--disable-background-networking"
                        "--allow-file-access-from-files"; "--user-data-dir=" + Path.Combine(folder,"profile"); "--dump-dom"
                        Uri(Path.Combine(folder,"index.html")).AbsoluteUri] do browser.StartInfo.ArgumentList.Add arg
            if not (browser.Start()) then failwith "Could not start Chromium"
            let output, errors = browser.StandardOutput.ReadToEndAsync(), browser.StandardError.ReadToEndAsync()
            if not (browser.WaitForExit 60000) then
                browser.Kill(true)
                browser.WaitForExit()
                failwith "Chromium timed out"
            let dom, stderr = output.Result, errors.Result
            Expect.equal browser.ExitCode 0 $"Chromium failed: {stderr}"
            let result = Regex.Match(dom, "<pre id=\"touchstick-names-result\">([^<]*)</pre>")
            if not result.Success then failwith $"Incomplete browser result: {dom}\n{stderr}"
            JsonDocument.Parse(System.Net.WebUtility.HtmlDecode result.Groups.[1].Value)
        finally
            Directory.Delete(folder,true)

    let private handlers names =
        [ for name in names do
            yield TouchStick.onTouchStickStart name (fun p -> Start(name,p))
            yield TouchStick.onTouchStickMove name (fun p -> Move(name,p))
            yield TouchStick.onTouchStickStop name (fun () -> Stop name) ]
        |> List.map (fun (name,value) ->
            match value with AttributeValue.Event event -> name,event | _ -> failwith "Expected TouchStick event") |> Map.ofList
    let private expected names =
        let start name x = Start(name,V2d(x,0.45))
        let move name distance x y = Move(name,{distance=distance;angle=Math.Atan2(y,x)*180.0/Math.PI})
        match names with
        | [name] -> [start name -0.29; move name 0.1 3.0 4.0; move name 1.0 3.0 4.0; Stop name;
                     start name -0.29; move name 0.1 -3.0 -4.0; Stop name]
        | [left;right] -> [start right 0.51; start left -0.29; move left 0.1 3.0 4.0; move right 0.1 -6.0 -8.0;
                          Stop left; move right 0.05 -4.0 -3.0; Stop right]
        | [] -> []
        | _ -> failwith "Unknown scenario"
    let private checkPackets id names (packets : JsonElement) =
        let events = handlers names
        let actual = [
            for packet in packets.EnumerateArray() do
                let sender, name = packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString()
                Expect.equal sender id "Use the handler element, not the child target"
                Expect.isTrue (events.ContainsKey name) $"Exact literal event name: {name}"
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
                if name.StartsWith "touchstickstop_" then Expect.equal args ["0"] "Unchanged stop payload"
                yield! events.[name].serverSide Guid.Empty sender args
        ]
        let wanted = expected names
        Expect.equal actual.Length wanted.Length "Every start/move/stop packet arrives exactly once"
        for a,b in List.zip actual wanted do
            match a,b with
            | Start(an,ap), Start(bn,bp) ->
                Expect.equal an bn "Literal start name and order"
                Expect.isLessThan (ap-bp).Length 1e-10 "Independent host-relative coordinates"
            | Move(an,ap), Move(bn,bp) ->
                Expect.equal an bn "Literal movement name and order"
                Expect.isLessThan (abs(ap.distance-bp.distance)) 1e-10 "Independent normalized/clamped movement"
                Expect.isLessThan (abs(ap.angle-bp.angle)) 1e-10 "Independent signed movement angle"
            | Stop an, Stop bn -> Expect.equal an bn "Literal stop name and order"
            | _ -> failtest $"Unexpected event {a}; expected {b}"

    let private run group () =
        match Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM" with
        | null | "" -> skiptest "Set AARDVARK_CHROMIUM to run rendered TouchStick name tests"
        | executable ->
            let rows = cases |> List.find (fst >> (=) group) |> snd
            use document = render executable rows
            let root = document.RootElement
            Expect.equal (root.GetProperty("errors").GetArrayLength()) 0 "No global browser errors"
            let results = root.GetProperty("results").EnumerateArray() |> Seq.toList
            Expect.equal (results |> List.map (fun r -> r.GetProperty("id").GetString())) (List.map fst rows) "All requested browser cases completed"
            let failures = [
                for row, (id,names) in List.zip results rows do
                    for field in ["errors";"problems"] do
                        for item in row.GetProperty(field).EnumerateArray() do yield id + ": " + item.GetString()
                    try checkPackets id names (row.GetProperty "packets")
                    with error -> yield id + ": " + error.Message
            ]
            Expect.isEmpty failures (String.concat "\n" failures)

    [<Tests>]
    let tests = testList "Primitives.TouchStick.Names" [
        testCase "Escaped names retain literal browser gesture identity" (run "escaped")
        testCase "Ordinary quoted Unicode and empty names retain behavior" (run "controls")
        testCase "Multiple sticks and empty configuration remain independent" (run "multiple")
        test "Numeric configuration and resource contract stay unchanged" {
            Expect.equal (boot "host" ["plain"])
                "if (typeof initTouchSticks === 'function') {    initTouchSticks('host',[{name:'plain',minx:-1.000000,maxx:1.000000,miny:-1.000000,maxy:1.000000,maxr:50.000000}]);} else {    console.warn('Cannot initialize TouchSticks (initTouchSticks undefined).');}"
                "Preserve the single-quoted property, numeric formatting, bounds, radius and initialization guard"
        }
    ]
