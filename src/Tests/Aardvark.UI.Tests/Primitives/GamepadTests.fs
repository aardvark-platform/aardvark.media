namespace Aardvark.UI.Tests

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Reflection
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open Expecto

module ``Gamepad Tests`` =
    type private Analog = { Player : int; Control : string; Values : float list }

    let private handlers =
        [ for player in 0 .. 3 do
            yield Gamepad.onLeftStickChanged player (fun v -> [{ Player = player; Control = "leftstick"; Values = [v.X; v.Y] }])
            yield Gamepad.onRightStickChanged player (fun v -> [{ Player = player; Control = "rightstick"; Values = [v.X; v.Y] }])
            yield Gamepad.onLeftTriggerChanged player (fun v -> [{ Player = player; Control = "leftshoulder"; Values = [v] }])
            yield Gamepad.onRightTriggerChanged player (fun v -> [{ Player = player; Control = "rightshoulder"; Values = [v] }]) ]
        |> List.map (function name, AttributeValue.Event handler -> name, handler | _ -> failwith "Expected Gamepad handler")
        |> Map.ofList

    let private mouse =
        match onMouseMoveAbs (fun position size -> position, size) with
        | _, AttributeValue.Event handler -> handler
        | _ -> failwith "Expected mouse handler"

    let private resource (assembly : Assembly) suffix =
        let name = assembly.GetManifestResourceNames() |> Array.filter (fun n -> n.EndsWith(suffix, StringComparison.Ordinal)) |> Array.exactlyOne
        use input = assembly.GetManifestResourceStream name
        use reader = new StreamReader(input)
        reader.ReadToEnd()

    let private writeFixture directory =
        for file, assembly, suffix in [
            "aardvark.js", typeof<DomNode<unit>>.Assembly, "resources.aardvark.js"
            "gamepad.js", typeof<CameraControllerState>.Assembly, "resources.gamepad.js"
            "gamepad-players-browser.js", Assembly.GetExecutingAssembly(), "gamepad-players-browser.js"
        ] do File.WriteAllText(Path.Combine(directory, file), resource assembly suffix)
        let attributes id =
            handlers |> Map.toList |> List.map (fun (name, handler) ->
                let code =
                    "if(this!==document.activeElement)throw new Error('Unbound gamepad receiver');trace.push('before:'+this.id);" +
                    Event.toString id name handler + ";trace.push('after:'+this.id);"
                $"{name}=\"{WebUtility.HtmlEncode code}\"") |> String.concat " "
        let mouseCode = Event.toString "gamepad-host" "onmousemove" mouse |> WebUtility.HtmlEncode
        let bootstrap = """
<script>
window.scriptErrors=[];window.packets=[];window.trace=[];
addEventListener('error',e=>scriptErrors.push(e.message||'resource error'),true);
addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));
console.warn=(...args)=>scriptErrors.push(args.join(' '));
window.nextGamepadFrame=null;window.scheduledFrames=0;window.gamepadPolls=0;
window.requestAnimationFrame=callback=>{if(nextGamepadFrame)throw new Error('Extra frame scheduled');nextGamepadFrame=callback;return ++scheduledFrames;};
window.cancelAnimationFrame=()=>{nextGamepadFrame=null;};
window.snapshots=[null,null,null,null];
Object.defineProperty(navigator,'getGamepads',{value:()=>{gamepadPolls++;return snapshots;}});
window.WebSocket=class{constructor(){window.gamepadSocket=this;this.readyState=3;}send(text){if(text!=='#ping'){const packet=JSON.parse(text);packets.push(packet);trace.push('send:'+packet.sender);}}};
</script>"""
        let hostAttributes, alternateAttributes = attributes "gamepad-host", attributes "gamepad-alternate"
        let control =
            $"<div id='gamepad-host' tabindex='0' {hostAttributes} style='position:absolute;left:40px;top:50px;width:200px;height:120px' onmousemove=\"if(this.id!=='gamepad-host'||event.currentTarget!==this||event.target.id!=='gamepad-child')throw new Error('Wrong inline receiver');trace.push('receiver:host');trace.push('before:gamepad-host');{mouseCode};trace.push('after:gamepad-host');\">" +
            "<div id='gamepad-child' style='position:absolute;left:50px;top:20px;width:40px;height:30px'>child</div></div>" +
            $"<div id='gamepad-alternate' tabindex='0' {alternateAttributes} style='position:absolute;left:400px;top:50px;width:200px;height:120px'></div>"
        File.WriteAllText(Path.Combine(directory, "index.html"),
            "<!doctype html><html><head><meta charset='utf-8'>" + bootstrap + "<script src='aardvark.js'></script><script src='gamepad.js'></script></head><body>" +
            control + "<script src='gamepad-players-browser.js'></script></body></html>")

    let private runBrowser () =
        let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then skiptest "Set AARDVARK_CHROMIUM to run genuine Chromium gamepad coverage."
        Expect.isTrue (File.Exists executable) "AARDVARK_CHROMIUM must name an executable"
        let directory = Path.Combine(Path.GetTempPath(), "aardvark-gamepad-players-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory directory |> ignore
        try
            writeFixture directory
            let start = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            for argument in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage";
                             "--allow-file-access-from-files"; "--no-first-run"; "--disable-background-networking";
                             "--user-data-dir=" + Path.Combine(directory, "profile"); "--dump-dom"; Uri(Path.Combine(directory, "index.html")).AbsoluteUri] do
                start.ArgumentList.Add argument
            use chromium = Process.Start start
            let output, errors = chromium.StandardOutput.ReadToEndAsync(), chromium.StandardError.ReadToEndAsync()
            if not (chromium.WaitForExit 60000) then
                chromium.Kill(true)
                chromium.WaitForExit()
                failtest "Chromium timed out"
            Expect.equal chromium.ExitCode 0 $"Chromium failed: {errors.Result}"
            let result = Regex.Match(output.Result, "<pre id=\"gamepad-results\">([^<]+)</pre>")
            Expect.isTrue result.Success $"Missing completed browser results: {output.Result}\n{errors.Result}"
            use document = JsonDocument.Parse(WebUtility.HtmlDecode result.Groups.[1].Value)
            let failures = document.RootElement.GetProperty("failures").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
            Expect.isEmpty failures (String.concat "\n" failures)
            document.RootElement.Clone()
        finally
            Directory.Delete(directory, true)

    let private checkStages (stages : JsonElement) =
        let failures = [
            for stage in stages.EnumerateArray() do
                let expected = [
                    for wanted in stage.GetProperty("expected").EnumerateArray() do
                        let player = wanted.GetProperty("player").GetInt32()
                        let control = wanted.GetProperty("control").GetString()
                        let values = wanted.GetProperty("values").EnumerateArray() |> Seq.map _.GetDouble() |> Seq.toList
                        yield wanted.GetProperty("sender").GetString(), $"gp_{control}_changed_{player}", values.Length,
                              [{ Player = player; Control = control; Values = values }] ]
                let actual = [
                    for packet in stage.GetProperty("packets").EnumerateArray() do
                        let sender, name = packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString()
                        let args = packet.GetProperty("args").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
                        yield sender, name, args.Length, handlers.[name].serverSide Guid.Empty sender args |> Seq.toList ]
                if actual <> expected then
                    let phase = stage.GetProperty("phase").GetString()
                    yield $"{phase}: expected ordered player/payload notifications {expected}, got {actual}"
        ]
        Expect.isEmpty failures (String.concat "\n" failures)

    [<Tests>]
    let tests =
        let result = lazy (runBrowser())
        testList "Primitives.Gamepad" [
            for control in ["leftstick"; "rightstick"; "leftshoulder"; "rightshoulder"] do
                testCase $"{control} changes and neutral values stay independent per player" (fun () ->
                    let stages = result.Value.GetProperty(control)
                    Expect.equal (stages.GetArrayLength()) (if control.EndsWith("stick", StringComparison.Ordinal) then 67 else 34) "Complete multiplayer and single-player matrix"
                    checkStages stages)
            testCase "Polling preserves routing binding dead zones and bubbling" (fun () ->
                let root = result.Value
                Expect.equal (root.GetProperty("controls").GetArrayLength()) 15 "Complete routing and polling controls"
                checkStages (root.GetProperty "controls")
                let packets = root.GetProperty "bubbling"
                Expect.equal (packets.GetArrayLength()) 1 "One bubbling packet"
                let packet = packets.[0]
                Expect.equal (packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString()) ("gamepad-host", "onmousemove") "Generated bubbling destination"
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
                Expect.equal (mouse.serverSide Guid.Empty "gamepad-host" args |> Seq.toList) [V2d(71.0, 33.0), V2d(200.0, 120.0)] "Host-relative server coordinates")
        ]
