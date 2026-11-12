namespace Aardvark.UI.Tests

open System
open System.IO
open System.Net
open System.Diagnostics
open System.Collections.Generic
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Updaters
open FSharp.Data.Adaptive
open Expecto

module ``Textbox Tests`` =
    type private Message = Value of string | Position of V2d * V2d
    type private Case =
        { Name: string; Pattern: string option; Initial: string; Accepted: string
          Rejected: string option; Server: string; Empty: bool; Regression: bool }

    let private cases = [|
        yield { Name="URL"; Pattern=Some "^https?://[^/]+/.*$"; Initial="https://old.test/a"; Accepted="https://new.test/path?q=1"; Rejected=Some "wrong"; Server="https://server.test/z"; Empty=false; Regression=true }
        yield { Name="path"; Pattern=Some "^/[^/]+/.*$"; Initial="/old/a"; Accepted="/new/b"; Rejected=Some "wrong"; Server="/server/z"; Empty=false; Regression=true }
        for name, terminator in ["LF", '\n'; "CR", '\r'; "LS", '\u2028'; "PS", '\u2029'] do
            yield { Name=name; Pattern=Some ("^[a-z" + string terminator + "]*$"); Initial="seed"; Accepted="next"; Rejected=Some "123"; Server="server"; Empty=true; Regression=true }
        yield { Name="ordinary"; Pattern=Some "^(|[a-z]+)$"; Initial="seed"; Accepted="next"; Rejected=Some "123"; Server="server"; Empty=true; Regression=false }
        yield { Name="escaped-slash"; Pattern=Some @"^\/[a-z]+\/[a-z]+$"; Initial="/old/a"; Accepted="/new/b"; Rejected=Some "wrong"; Server="/server/z"; Empty=false; Regression=false }
        yield { Name="quotes-backslashes"; Pattern=Some """^["'\\]+$"""; Initial="\""; Accepted="'\\\""; Rejected=Some "wrong"; Server="\\'"; Empty=false; Regression=false }
        yield { Name="unrestricted"; Pattern=None; Initial="seed"; Accepted="anything / \" 東京"; Rejected=None; Server="server / 東京"; Empty=true; Regression=false }
    |]

    let private resource (assembly: Reflection.Assembly) suffix =
        let name = assembly.GetManifestResourceNames() |> Array.find (fun name -> name.EndsWith(suffix, StringComparison.Ordinal))
        use stream = assembly.GetManifestResourceStream name
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let private setup (case: Case) =
        let value = cval case.Initial
        let handlers = EventHandlers<Message>()
        let event = FSharp.Control.Event<Message>()
        let state =
            { scenes=ContraDict.ofDictionary(Dictionary<_,_>()); handlers=handlers :> ContraDict<_,_>
              references=Dictionary<_,_>(); channels=Dictionary<_,_>(); messages=event.Publish }
        let _, mouse = onMouseDownAbs (fun _ position size -> Position(position,size))
        let mouse = match mouse with AttributeValue.Event e -> e | _ -> failwith "Mouse event missing"
        let parent = { mouse with clientSide = fun send id -> "window.beforeBubble(this,event);" + mouse.clientSide send id + "window.afterBubble(this,event);" }
        let attributes = AttributeMap.ofList [
            style "position:absolute;left:40px;top:50px;width:200px;height:120px;padding:0;border:0;margin:0"
            "onchange", AttributeValue.Event parent
        ]
        let node = SimplePrimitives.Incremental.textbox { TextConfig.empty with regex=case.Pattern; maxLength=Some 73 } attributes value Value
        let updater = node.NewUpdater(Unchecked.defaultof<IHttpRequest>)
        let code = updater.Update(AdaptiveToken.Top,state,ValueSome(fun self -> JSExpr.AppendChild(JSExpr.Body,self))) |> JSExpr.toString
        let reader = state.channels.[ChannelId(updater.Id,"valueCh")]
        let initial = reader.GetMessages AdaptiveToken.Top
        transact (fun () -> value.Value <- case.Server)
        let pushed = reader.GetMessages AdaptiveToken.Top
        let dispose () =
            updater.Destroy(state,JSExpr.GetElementById updater.Id) |> ignore
            reader.Dispose()
        node, updater.Id, code, initial, pushed, handlers, dispose

    let private browser () =
        let executable = Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then Tests.skiptest "Set AARDVARK_CHROMIUM to run the real Chromium textbox regressions"
        let root = Path.Combine(Path.GetTempPath(),$"aardvark-textbox-{Guid.NewGuid():N}")
        Directory.CreateDirectory root |> ignore
        let fixtures = cases |> Array.map setup
        try
            let ui = typeof<DomNode<string>>.Assembly
            File.WriteAllText(Path.Combine(root,"jquery.js"),resource ui "jquery-4.0.0.min.js")
            File.WriteAllText(Path.Combine(root,"aardvark.js"),resource ui "resources.aardvark.js")
            File.WriteAllText(Path.Combine(root,"test.js"),resource typeof<Case>.Assembly "textbox-browser.js")
            let config = Array.map2 (fun case (_,id,(code: string),initial,pushed,_,_) ->
                let index = Array.findIndex ((=) case) cases
                let file = $"widget-{index}.js"
                File.WriteAllText(Path.Combine(root,file),code)
                {| name=case.Name; id=id; script=file; pattern=Option.toObj case.Pattern; initial=case.Initial
                   accepted=case.Accepted; rejected=Option.toObj case.Rejected; server=case.Server; empty=case.Empty
                   initialMessages=initial; serverMessages=pushed |}) cases fixtures
            let json = JsonSerializer.Serialize config
            let html = "<!doctype html><html><head><meta charset='utf-8'><style>body{margin:0}input{position:absolute;left:50px;top:20px;width:40px;height:30px;padding:0;border:0;margin:0;box-sizing:border-box}</style>" +
                       "<script>window.scriptErrors=[];addEventListener('error',e=>scriptErrors.push(String(e.error||e.message)));addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));</script>" +
                       "<script src='jquery.js'></script><script src='aardvark.js'></script></head><body><script>window.fixtures=" + json + ";</script><script src='test.js'></script></body></html>"
            File.WriteAllText(Path.Combine(root,"index.html"),html)
            let start = ProcessStartInfo(executable,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true)
            for arg in ["--headless";"--no-sandbox";"--disable-gpu";"--disable-dev-shm-usage";"--disable-background-networking";"--no-first-run";"--no-default-browser-check";"--disable-extensions";"--disable-sync";"--incognito";"--allow-file-access-from-files";"--virtual-time-budget=10000";"--dump-dom";"--user-data-dir=" + Path.Combine(root,"profile");Uri(Path.Combine(root,"index.html")).AbsoluteUri] do start.ArgumentList.Add arg
            use child = new Process(StartInfo=start)
            child.Start() |> ignore
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            if not (child.WaitForExit 60000) then
                child.Kill(true)
                child.WaitForExit()
                failtest ("Chromium timed out: " + errors.GetAwaiter().GetResult())
            let text = output.GetAwaiter().GetResult()
            Expect.equal child.ExitCode 0 ("Chromium failed: " + errors.GetAwaiter().GetResult())
            let matched = Regex.Match(text,"<pre id=\"textbox-result\">(.*?)</pre>",RegexOptions.Singleline)
            Expect.isTrue matched.Success "Browser must produce its complete result marker"
            use document = JsonDocument.Parse(WebUtility.HtmlDecode matched.Groups.[1].Value)
            let results = document.RootElement.GetProperty("cases").EnumerateArray() |> Seq.map _.Clone() |> Seq.toArray
            Expect.equal results.Length cases.Length "Every case must complete"
            Array.map3 (fun case (_,id,_,_,_,(handlers: EventHandlers<Message>),_) (result: JsonElement) ->
                let mutable values = []
                let mutable positions = 0
                for packet in result.GetProperty("packets").EnumerateArray() do
                    let sender = packet.GetProperty("sender").GetString()
                    let name = packet.GetProperty("name").GetString()
                    let version = match packet.TryGetProperty "version" with true,v -> ValueSome(v.GetByte()) | _ -> ValueNone
                    match handlers.TryGet(ChannelId(sender,name),version), packet.TryGetProperty "args" with
                    | ValueSome handler, (true,args) ->
                        for message in transact (fun () -> handler.invoke Guid.Empty sender (args.EnumerateArray() |> Seq.map _.GetString() |> Seq.toList) |> Seq.toList) do
                            match message with
                            | Value value ->
                                Expect.equal sender id "Textbox callback sender"
                                values <- values @ [value]
                            | Position(position,size) ->
                                Expect.equal position (V2d(71.0,33.0)) "Parent-local coordinates, not child-local coordinates"
                                Expect.equal size (V2d(200.0,120.0)) "Parent geometry"
                                positions <- positions + 1
                    | _ -> ()
                let expected = [
                    yield case.Accepted
                    if case.Empty then yield ""
                    yield case.Accepted
                    yield case.Accepted
                ]
                let failure = result.GetProperty("failure").GetString()
                if String.IsNullOrEmpty failure then
                    Expect.equal values expected "Actual serialized packets decode through the public helper callback"
                    Expect.equal positions (if case.Rejected.IsSome then 7 else 4) "All native bubbled change events reach the parent"
                case.Name, case.Regression, failure) cases fixtures results
        finally
            for _,_,_,_,_,_,dispose in fixtures do dispose()
            Directory.Delete(root,true)

    let private observed = lazy (browser())
    let private checkBrowser regression =
        let failures = observed.Value |> Array.choose (fun (name,kind,error) -> if kind=regression && not(String.IsNullOrEmpty error) then Some(name+": "+error) else None)
        if failures.Length > 0 then failtest (String.concat "\n" failures)

    [<Tests>]
    let tests = testList "Primitives.Textbox" [
        test "Raw slashes and all JavaScript line terminators initialize and validate" { checkBrowser true }
        test "Ordinary escaped quoted and unrestricted patterns retain browser behavior" { checkBrowser false }
        test "Public callbacks remain deferred and preserve exact values" {
            let mutable calls = 0
            let node = SimplePrimitives.Incremental.textbox TextConfig.empty AttributeMap.empty (AVal.constant "server") (fun value -> calls <- calls + 1; value)
            let inner = node :?> InnerNode<string>
            let event = match (AVal.force inner.Attributes.Content).["data-event"] with AttributeValue.Event e -> e | _ -> failwith "Callback missing"
            for value in [""; "/a/b"; "quotes \"' and \\"; "\n\r\u2028\u2029"; "東京"] do
                let before = calls
                let messages = event.serverSide Guid.Empty "textbox" [JsonSerializer.Serialize value; "ignored"]
                Expect.equal calls before "Callback stays deferred until message enumeration"
                Expect.equal (transact (fun () -> Seq.toList messages)) [value] "Preserve decoded string, including empty and literal delimiters"
                Expect.equal calls (before + 1) "Exactly one callback"
            Expect.isEmpty (event.serverSide Guid.Empty "textbox" []) "Missing arguments produce no messages"
            Expect.equal calls 5 "Missing arguments do not invoke the callback"
        }
        test "Patterns use one encoded construction outside per-input validation" {
            for case in cases do
                let node,_,_,_,_,_,dispose = setup case
                try
                    let code = node.Boot.Value "transport"
                    match case.Pattern with
                    | Some expected ->
                        let pattern = "new RegExp\\((\"(?:\\\\.|[^\"\\\\])*\")\\)"
                        let found = Regex.Matches(code,pattern)
                        Expect.equal found.Count 1 "One RegExp is constructed per initialization"
                        Expect.equal (JsonSerializer.Deserialize<string>(found.[0].Groups.[1].Value)) expected "Exact public-helper pattern transport"
                        Expect.isLessThan (code.IndexOf("new RegExp",StringComparison.Ordinal)) (code.IndexOf("function(a)",StringComparison.Ordinal)) "Construction precedes per-input validation"
                    | None -> Expect.isFalse (code.Contains "new RegExp") "Unrestricted textboxes do not create a regex"
                finally dispose()
        }
    ]
