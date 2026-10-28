namespace Aardvark.UI.Tests

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Net
open System.Reflection
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``Gamepad Tests`` =
    let private initial =
        { FreeFlyController.initial with
            moveVec = V3d(0.0, 0.25, 0.0)
            freeFlyConfig = { FreeFlyController.initial.freeFlyConfig with touchScalesExponentially = false } }

    let private handlers =
        FreeFlyController.attributes (AdaptiveCameraControllerState.Create initial) id
        |> _.Content |> AVal.force |> HashMap.toSeq
        |> Seq.choose (fun (name, value) ->
            match value with
            | AttributeValue.Event handler when name = "gp_leftstick_changed_0" || name = "gp_rightstick_changed_0" -> Some (name, handler)
            | _ -> None)
        |> Map.ofSeq

    let private mouse =
        match onMouseMoveAbs (fun position size -> position, size) with
        | _, AttributeValue.Event handler -> handler
        | _ -> failwith "Expected generated mouse handler"

    let private resource (assembly: Assembly) suffix =
        let name = assembly.GetManifestResourceNames() |> Array.filter (fun n -> n.EndsWith(suffix, StringComparison.Ordinal)) |> Array.exactlyOne
        use input = assembly.GetManifestResourceStream name
        use reader = new StreamReader(input)
        reader.ReadToEnd()

    let private writeFixture directory =
        for file, assembly, suffix in [
            "aardvark.js", typeof<DomNode<unit>>.Assembly, "resources.aardvark.js"
            "gamepad.js", typeof<CameraControllerState>.Assembly, "resources.gamepad.js"
            "gamepad-browser.js", Assembly.GetExecutingAssembly(), "gamepad-browser.js"
        ] do File.WriteAllText(Path.Combine(directory, file), resource assembly suffix)
        let attributes =
            handlers |> Map.toList |> List.map (fun (name, handler) ->
                let code = Event.toString "gamepad-host" name handler |> WebUtility.HtmlEncode
                $"{name}=\"{code}\"") |> String.concat " "
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
window.gamepadSnapshot={index:0,connected:true,buttons:[],axes:[0,0,0,0]};
Object.defineProperty(navigator,'getGamepads',{value:()=>{gamepadPolls++;return [gamepadSnapshot,null,null,null];}});
window.WebSocket=class{constructor(){window.gamepadSocket=this;this.readyState=3;}send(text){if(text!=='#ping'){const packet=JSON.parse(text);packets.push(packet);if(packet.name==='onmousemove')trace.push('send:host');}}};
</script>"""
        let control =
            $"<div id='gamepad-host' tabindex='0' {attributes} style='position:absolute;left:40px;top:50px;width:200px;height:120px' onmousemove=\"if(this.id!=='gamepad-host'||event.currentTarget!==this||event.target.id!=='gamepad-child')throw new Error('Wrong inline receiver');trace.push('receiver:host');trace.push('before');{mouseCode};trace.push('after');\">" +
            "<div id='gamepad-child' style='position:absolute;left:50px;top:20px;width:40px;height:30px'>child</div></div>"
        File.WriteAllText(Path.Combine(directory, "index.html"),
            "<!doctype html><html><head><meta charset='utf-8'>" + bootstrap + "<script src='aardvark.js'></script><script src='gamepad.js'></script></head><body>" +
            control + "<script src='gamepad-browser.js'></script></body></html>")

    let private runBrowser () =
        let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then
            skiptest "Set AARDVARK_CHROMIUM to an explicit Chromium executable (required in Linux CI)."
        Expect.isTrue (File.Exists executable) "AARDVARK_CHROMIUM must name an executable"
        let directory = Path.Combine(Path.GetTempPath(), "aardvark-gamepad-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory directory |> ignore
        try
            writeFixture directory
            let start = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            for argument in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage";
                             "--allow-file-access-from-files"; "--no-first-run"; "--disable-background-networking";
                             "--virtual-time-budget=10000"; "--user-data-dir=" + Path.Combine(directory, "profile"); "--dump-dom"; Uri(Path.Combine(directory, "index.html")).AbsoluteUri] do
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

    let private vector (element: JsonElement) =
        V2d(element.[0].GetDouble(), element.[1].GetDouble())

    let private checkStages name (stages: JsonElement) =
        let mutable model = initial
        let mutable previousLeft = V2d.Zero
        let mutable previousRight = V2d.Zero
        for stage in stages.EnumerateArray() do
            let phase = name + ": " + stage.GetProperty("phase").GetString()
            let left, right = vector (stage.GetProperty "left"), vector (stage.GetProperty "right")
            let packets = stage.GetProperty("packets").EnumerateArray() |> Seq.toList
            let expected = [
                if left <> previousLeft then "gp_leftstick_changed_0", left
                if right <> previousRight then "gp_rightstick_changed_0", right
            ]
            for packet in packets do
                Expect.equal (packet.GetProperty("sender").GetString()) "gamepad-host" $"{phase}: event receiver"
                let name = packet.GetProperty("name").GetString()
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
                let messages = handlers.[name].serverSide Guid.Empty "gamepad-host" args |> Seq.toList
                Expect.equal messages.Length 1 $"{phase}: one FreeFly message per packet"
                model <- List.fold FreeFlyController.update model messages
            // Linear configured scales give an independent Cartesian oracle, including the
            // untouched vertical movement and opposite stick, rather than reusing TouchStick.
            let expectedMove = V3d(3.125 * left.X, 0.25, -3.125 * left.Y)
            let expectedRotation = V3d(-0.01875 * right.Y, -0.01875 * right.X, 0.0)
            Expect.isLessThan (model.moveVec - expectedMove).Length 1E-12 $"{phase}: FreeFly movement must resume after release"
            Expect.isLessThan (model.rotateVec - expectedRotation).Length 1E-12 $"{phase}: FreeFly rotation must resume after release"
            let actual =
                packets |> List.map (fun packet ->
                    let args = packet.GetProperty("args").EnumerateArray() |> Seq.map (fun v -> Double.Parse(v.GetString(), CultureInfo.InvariantCulture)) |> Seq.toArray
                    Expect.equal args.Length 2 $"{phase}: two unchanged coordinate arguments"
                    packet.GetProperty("name").GetString(), V2d(args.[0], args.[1]))
            Expect.equal actual expected $"{phase}: exact coordinates, order and held-value deduplication"
            previousLeft <- left
            previousRight <- right

    [<Tests>]
    let tests =
        // One browser run feeds independent polling and server-state assertions.
        let result = lazy (runBrowser())
        testList "Gamepad Tests" [
            for stick in ["left"; "right"] do
                testCase $"{stick} stick restores FreeFly after identical reengagement" (fun () ->
                    let scenarios = result.Value.GetProperty(stick)
                    Expect.equal (scenarios.GetArrayLength()) 6 "All signed and diagonal directions must complete"
                    for scenario in scenarios.EnumerateArray() do
                        let stages = scenario.GetProperty "stages"
                        let name = scenario.GetProperty("name").GetString()
                        Expect.equal (stages.GetArrayLength()) 11 name
                        checkStages name stages)
            testCase "Polling preserves dead zones, stick isolation and bubbling" (fun () ->
                let root = result.Value
                let stages = root.GetProperty "controls"
                Expect.equal (stages.GetArrayLength()) 14 "All polling controls must complete"
                checkStages "polling controls" stages
                let packets = root.GetProperty "bubbling"
                Expect.equal (packets.GetArrayLength()) 1 "One bubbling packet"
                let packet = packets.[0]
                Expect.equal (packet.GetProperty("sender").GetString(), packet.GetProperty("name").GetString()) ("gamepad-host", "onmousemove") "Generated bubbling destination"
                let args = packet.GetProperty("args").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
                Expect.equal (mouse.serverSide Guid.Empty "gamepad-host" args |> Seq.toList) [V2d(71.0, 33.0), V2d(200.0, 120.0)] "Independent server-side bubbling coordinates")
        ]
