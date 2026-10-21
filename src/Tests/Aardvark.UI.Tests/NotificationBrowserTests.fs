namespace Aardvark.UI.Tests

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Application
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives.Notifications
open FSharp.Data.Adaptive
open Expecto

module ``Notification Browser Tests`` =

    type private Action =
        | Send of view: int * message: string
        | Remove of view: int * id: int
        | Clear of view: int
        | Dismiss of view: int
        | ReplayRemove of view: int * id: int
        | Check of messages: string list list * callbacks: (int * int) list

    let private scenarios = [
        for first in [0; 1] do
            let other = 1 - first
            yield $"remove view {first}", false, [
                Send(0, "alpha"); Send(1, "beta")
                Check([["alpha"]; ["beta"]], [])
                Remove(first, 0)
                Check((if first = 0 then [[]; ["beta"]] else [["alpha"]; []]), [first, 0])
                Remove(other, 0)
                Check([[]; []], [first, 0; other, 0])
            ]
            yield $"clear view {first}", false, [
                Send(0, "alpha"); Send(1, "beta"); Send(first, "sibling")
                Clear first
                Check((if first = 0 then [[]; ["beta"]] else [["alpha"]; []]), [first, 0; first, 1])
                Remove(other, 0)
                Check([[]; []], [first, 0; first, 1; other, 0])
            ]
            yield $"dismiss view {first}", false, [
                Send(0, "alpha"); Send(1, "beta")
                Dismiss first
                Check((if first = 0 then [[]; ["beta"]] else [["alpha"]; []]), [first, 0])
                Remove(other, 0)
                Check([[]; []], [first, 0; other, 0])
            ]
        yield "shared state broadcast", true, [
            Send(0, "shared"); Check([["shared"]; ["shared"]], [])
            Remove(0, 0); Check([[]; []], [0, 0; 1, 0])
        ]
        yield "single container and sibling ID", false, [
            Send(0, "alpha"); Send(0, "sibling")
            Check([["alpha"; "sibling"]; []], [])
            Remove(0, 0); Check([["sibling"]; []], [0, 0])
            Remove(0, 1); Check([[]; []], [0, 0; 0, 1])
        ]
        yield "unknown and already removed IDs", false, [
            Send(0, "alpha"); Remove(1, 0); Remove(0, 99)
            Check([["alpha"]; []], [])
            Remove(0, 0); ReplayRemove(0, 0); ReplayRemove(1, 0)
            Check([[]; []], [0, 0])
        ]
    ]

    let private fixture index (name, shared, actions) =
        let owners = [| $"notifications-{index}-a"; $"notifications-{index}-b" |]
        let models = [| AdaptiveNotifications Notifications.Empty; AdaptiveNotifications Notifications.Empty |]
        if shared then models.[1] <- models.[0]
        let states = [| Notifications.Empty; Notifications.Empty |]
        let handlers = ResizeArray<string * Event<string * Notifications.Message>>()
        let boots =
            owners |> Array.mapi (fun view owner ->
                let node =
                    Notifications.container (fun msg -> owner, msg)
                        (fun attributes ->
                            match attributes |> List.find (fst >> (=) "onremove") |> snd with
                            | AttributeValue.Event handler -> handlers.Add(owner, handler)
                            | _ -> failwith "Expected the actual removal handler"
                            div attributes []) models.[view]
                node)
        let readers = boots |> Array.map (fun node -> node.Channels.["channelNotify"].GetReader())
        try
            for reader in readers do
                Expect.isEmpty (reader.GetMessages AdaptiveToken.Top) "Empty notifications must emit nothing"
            let steps = ResizeArray<obj>()
            for action in actions do
                match action with
                | Dismiss view -> steps.Add {| kind = "dismiss"; target = owners.[view] |}
                | ReplayRemove(view, id) -> steps.Add {| kind = "replay"; target = owners.[view]; id = id |}
                | Check(messages, callbacks) ->
                    steps.Add {| kind = "check"; messages = messages
                                 callbacks = callbacks |> List.map (fun (view, id) -> {| sender = owners.[view]; id = id |}) |}
                | _ ->
                    let view, message =
                        match action with
                        | Send(view, content) -> view, Notifications.Send (notification {
                            message content
                            closeIcon true
                            duration Duration.Infinite
                          })
                        | Remove(view, id) -> view, Notifications.Remove id
                        | Clear view -> view, Notifications.Clear
                        | _ -> failwith "Unexpected action"
                    states.[view] <- Notifications.update message states.[view]
                    transact (fun () -> models.[view].Update states.[view])
                    if shared then states.[1 - view] <- states.[view]
                    for target in 0 .. 1 do
                        for data in readers.[target].GetMessages AdaptiveToken.Top do
                            steps.Add {| kind = "deliver"; target = owners.[target]
                                         data = JsonSerializer.Deserialize<JsonElement> data |}
            let boot =
                boots |> Array.mapi (fun i node ->
                    match node.Boot with
                    | ValueSome boot -> $"{{ const channelNotify = aardvark.getChannel('{owners.[i]}', 'channelNotify'); {boot owners.[i]} }}"
                    | _ -> failwith "Expected notification boot code") |> String.concat "\n"
            {| name = name; owners = owners; boot = boot; steps = steps.ToArray() |}, List.ofSeq handlers
        finally
            for reader in readers do reader.Dispose()

    let private mouseHandler =
        match onMouseClickAbs (fun button position size -> button, position, size) with
        | _, AttributeValue.Event handler -> handler
        | _ -> failwith "Expected mouse event"

    let private copyResource (assembly: Reflection.Assembly) suffix destination =
        let name = assembly.GetManifestResourceNames() |> Array.filter (fun n -> n.EndsWith(suffix, StringComparison.Ordinal)) |> Array.exactlyOne
        use input = assembly.GetManifestResourceStream name
        use output = File.Create destination
        input.CopyTo output

    [<Tests>]
    let tests =
        test "Notifications are isolated in rendered Chromium containers" {
            let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
            if String.IsNullOrWhiteSpace executable then
                skiptest "Set AARDVARK_CHROMIUM to an explicit Chromium executable (required in Linux CI)."
            Expect.isTrue (File.Exists executable) "AARDVARK_CHROMIUM must name an existing executable"
            let directory = Path.Combine(Path.GetTempPath(), "aardvark-notifications-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory directory |> ignore
            try
                let fixtures, handlers = scenarios |> List.mapi fixture |> List.unzip
                let ui = typeof<DomNode<unit>>.Assembly
                let primitives = typeof<Notification>.Assembly
                for assembly, resource, filename in [
                    ui, "jquery-4.0.0.min.js", "jquery.js"
                    ui, "aardvark.js", "aardvark.js"
                    primitives, "resources/fomantic/semantic.js", "semantic.js"
                    primitives, "resources/fomantic/semantic.css", "semantic.css"
                    primitives, "notifications.js", "notifications.js"
                    typeof<Action>.Assembly, "notification-browser.js", "fixture.js"
                ] do copyResource assembly resource (Path.Combine(directory, filename))
                let mouse = Event.toString "receiver" "onclick" mouseHandler |> WebUtility.HtmlEncode
                let html =
                    "<!doctype html><html><head><meta charset='utf-8'><link rel='stylesheet' href='semantic.css'>" +
                    "<script>window.scriptErrors=[];addEventListener('error',e=>scriptErrors.push(e.message||'resource error'),true);" +
                    "addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));</script>" +
                    "<script src='jquery.js'></script><script src='aardvark.js'></script>" +
                    "<script src='semantic.js'></script><script src='notifications.js'></script></head><body>" +
                    $"<div id='receiver' style='position:absolute;left:40px;top:50px;width:200px;height:120px' onclick=\"if(this.id!=='receiver'||event.target.id!=='child'||event.currentTarget!==this)throw new Error('Wrong inline receiver');trace.push('receiver:'+this.id);trace.push('before');{mouse};trace.push('after');\">" +
                    "<span id='child' style='position:absolute;left:50px;top:20px;width:40px;height:30px'>child</span></div>" +
                    "<script>window.fixtures=" + JsonSerializer.Serialize fixtures + ";</script><script src='fixture.js'></script></body></html>"
                let path = Path.Combine(directory, "index.html")
                File.WriteAllText(path, html)
                let start = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
                for argument in ["--headless"; "--incognito"; "--no-sandbox"; "--disable-gpu"; "--disable-dev-shm-usage"; "--allow-file-access-from-files";
                                 "--no-first-run"; "--disable-background-networking"; "--virtual-time-budget=30000";
                                 "--user-data-dir=" + Path.Combine(directory, "profile"); "--dump-dom"; Uri(path).AbsoluteUri] do
                    start.ArgumentList.Add argument
                use browser = Process.Start start
                let output = browser.StandardOutput.ReadToEndAsync()
                let errors = browser.StandardError.ReadToEndAsync()
                if not (browser.WaitForExit 60000) then
                    browser.Kill(true)
                    browser.WaitForExit()
                    failtest "Chromium did not complete within 60 seconds"
                let output, errors = output.Result, errors.Result
                Expect.equal browser.ExitCode 0 $"Chromium failed: {errors}"
                let result = Regex.Match(output, "<pre id=\"notification-results\">([^<]+)</pre>")
                Expect.isTrue result.Success $"Missing completed browser results: {output}\n{errors}"
                use document = JsonDocument.Parse(WebUtility.HtmlDecode result.Groups.[1].Value)
                let root = document.RootElement
                Expect.equal (root.GetProperty("completed").GetInt32()) (scenarios.Length + 1) "All scenarios and the bubbling control must finish"
                let failures = root.GetProperty("failures").EnumerateArray() |> Seq.map string |> Seq.toList
                Expect.isEmpty failures (String.concat "\n" failures)
                let removalHandlers = handlers |> List.concat |> Map.ofList
                for packet in root.GetProperty("packets").EnumerateArray() do
                    let sender = packet.GetProperty("sender").GetString()
                    let args = packet.GetProperty("args").EnumerateArray() |> Seq.map (fun a -> a.GetString()) |> Seq.toList
                    if sender = "receiver" then
                        Expect.equal (mouseHandler.serverSide Guid.Empty sender args |> Seq.toList)
                            [MouseButtons.Left, V2d(71.0, 33.0), V2d(200.0, 120.0)] "Decode the actual bubbling packet"
                    else
                        Expect.equal (packet.GetProperty("name").GetString()) "onremove" "Actual removal callback name"
                        let expectedId = Int32.Parse (List.exactlyOne args)
                        Expect.equal (removalHandlers.[sender].serverSide Guid.Empty sender args |> Seq.toList)
                            [sender, Notifications.Remove expectedId] "Callback must map to its owning F# view"
            finally
                Directory.Delete(directory, true)
        }
