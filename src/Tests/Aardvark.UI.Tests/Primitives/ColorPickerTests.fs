namespace Aardvark.UI.Tests

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Net
open System.Reflection
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Updaters
open FSharp.Data.Adaptive
open Expecto

module ``ColorPicker Teardown Tests`` =
    type private Message = Color of C4b | Position of V2d * V2d

    let private resource (assembly : Assembly) suffix =
        let names = assembly.GetManifestResourceNames() |> Array.filter (fun n -> n.Replace('\\','.').Replace('/','.').EndsWith(suffix, StringComparison.Ordinal))
        if names.Length <> 1 then failwith $"Missing/ambiguous resource {suffix} in {assembly.FullName}: {assembly.GetManifestResourceNames()}"
        let name = names.[0]
        use stream = assembly.GetManifestResourceStream name
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let private fixture mode =
        let value = cval (C4b(17uy,34uy,51uy,68uy))
        let handlers = EventHandlers<Message>()
        let messages = FSharp.Control.Event<Message>()
        let state =
            { scenes=ContraDict.ofDictionary(Dictionary<_,_>()); handlers=handlers :> ContraDict<_,_>
              references=Dictionary<_,_>(); channels=Dictionary<_,_>(); messages=messages.Publish }
        let _, mouse = onMouseMoveAbs (fun p size -> Position(p,size))
        let mouse = match mouse with AttributeValue.Event e -> e | _ -> failwith "Missing mouse handler"
        let mouse = { mouse with clientSide = fun send id -> "beforeBubble(this,event);" + mouse.clientSide send id + "afterBubble(this,event);" }
        let children = clist<DomNode<Message>>()
        let root = Incremental.div (AttributeMap.ofList [style "position:absolute;left:40px;top:50px;width:200px;height:120px"; "onmousemove",AttributeValue.Event mouse]) children
        let updater = root.NewUpdater(Unchecked.defaultof<IHttpRequest>)
        let initial = updater.Update(AdaptiveToken.Top,state,ValueSome(fun self -> JSExpr.AppendChild(JSExpr.Body,self))) |> JSExpr.toString
        let config =
            { ColorPicker.Config.DefaultWithAlpha with
                displayMode = if mode="inline" then ColorPicker.DisplayMode.Inline elif mode="disabled" then ColorPicker.DisplayMode.Disabled else ColorPicker.DisplayMode.Dropdown
                pickerStyle = Some { ColorPicker.PickerStyle.DefaultWithAlpha with textInput=ColorPicker.TextInput.Enabled }
                palette=Some(ColorPicker.Palette.ofArray 2 [|C4b(32uy,64uy,128uy,128uy);C4b.Red|]); maxSelectionSize=0 }
        transact (fun () -> children.Append(ColorPicker.view config Color value) |> ignore)
        let create = updater.Update(AdaptiveToken.Top,state,ValueNone) |> JSExpr.toString
        let references = state.references.Values |> Seq.map (fun r -> {| name=r.name; kind=r.kind.ToString().ToLowerInvariant(); url=r.url |}) |> Seq.toArray
        let key = state.channels.Keys |> Seq.find (fun k -> k.ChannelName="valueCh")
        let reader = state.channels.[key]
        let channelInitial = reader.GetMessages AdaptiveToken.Top
        transact (fun () -> value.Value <- C4b(100uy,150uy,200uy,204uy))
        let channelUpdate = reader.GetMessages AdaptiveToken.Top
        let colorHandler = handlers.TryGet(ChannelId(key.ElementId,"data-event"),ValueSome 0uy) |> ValueOption.get
        let mouseHandler = handlers.TryGet(ChannelId(updater.Id,"onmousemove"),ValueSome 0uy) |> ValueOption.get
        transact (fun () -> children.Clear())
        let remove = updater.Update(AdaptiveToken.Top,state,ValueNone) |> JSExpr.toString
        let destroy = updater.Destroy(state,JSExpr.GetElementById updater.Id) |> JSExpr.toString
        {| mode=mode; id=key.ElementId; root=updater.Id; initial=initial; create=create; remove=remove
           finish=destroy + $"document.getElementById('{updater.Id}').remove();"; references=references; channelInitial=channelInitial; channelUpdate=channelUpdate |}, colorHandler, mouseHandler

    let private writeFixture directory asset =
        let cases =
            [| yield "survivor", "inline"
               for mode in ["closed";"open";"inline";"disabled"] do
                   for _ in 1..10 do yield "lifecycle",mode
               for mode in ["open";"inline";"closed";"disabled"] do yield "plugin",mode
               for mode in ["open";"inline"] do
                   for rail in ["color";"hue";"alpha"] do
                       for phase in ["active";"pending"] do yield "drag-"+rail+"-"+phase,mode
               yield "drag-color-doublepending","open"
               yield "compatibility","open" |]
        let rows = cases |> Array.map (fun (group,mode) -> let row,color,mouse=fixture mode in group,row,color,mouse)
        for file,assembly,suffix in [
            "jquery.js",typeof<DomNode<unit>>.Assembly,"jquery-4.0.0.min.js"
            "aardvark.js",typeof<DomNode<unit>>.Assembly,"resources.aardvark.js"
            "resources/spectrum.js",Assembly.GetExecutingAssembly(),"picker."+asset
            "resources/spectrum.css",typeof<ColorPicker.Config>.Assembly,"resources.spectrum.css"
            "resources/spectrum-overrides.css",typeof<ColorPicker.Config>.Assembly,"resources.spectrum-overrides.css"
            "test.js",Assembly.GetExecutingAssembly(),"color-picker-teardown-browser.js"
        ] do
            let path=Path.Combine(directory,file)
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
            File.WriteAllText(path,resource assembly suffix)
        let data=rows |> Array.map (fun (group,row,_,_) -> {| group=group; data=row |}) |> JsonSerializer.Serialize
        File.WriteAllText(Path.Combine(directory,"fixtures.js"),"window.fixtures="+data+";")
        File.WriteAllText(Path.Combine(directory,"index.html"),"<!doctype html><html><head><meta charset='utf-8'><script>window.scriptErrors=[];addEventListener('error',e=>scriptErrors.push(String(e.error||e.message)));addEventListener('unhandledrejection',e=>scriptErrors.push(String(e.reason)));</script><script src='jquery.js'></script><script src='aardvark.js'></script><script src='fixtures.js'></script></head><body><script src='test.js'></script></body></html>")
        rows

    let private runBrowser asset =
        let executable=Environment.GetEnvironmentVariable "AARDVARK_CHROMIUM"
        if String.IsNullOrWhiteSpace executable then skiptest "Set AARDVARK_CHROMIUM to run genuine ColorPicker teardown coverage"
        Expect.isTrue (File.Exists executable) "Configured Chromium must exist"
        let directory=Path.Combine(Path.GetTempPath(),"aardvark-picker-teardown-"+Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory directory |> ignore
        try
            let rows=writeFixture directory asset
            let start=ProcessStartInfo(executable,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true)
            for arg in ["--headless";"--incognito";"--no-sandbox";"--disable-gpu";"--disable-dev-shm-usage";"--no-first-run";"--disable-background-networking";"--allow-file-access-from-files";"--virtual-time-budget=10000";"--user-data-dir="+Path.Combine(directory,"profile");"--dump-dom";Uri(Path.Combine(directory,"index.html")).AbsoluteUri] do start.ArgumentList.Add arg
            use browser=Process.Start start
            let output,errors=browser.StandardOutput.ReadToEndAsync(),browser.StandardError.ReadToEndAsync()
            if not(browser.WaitForExit 60000) then
                browser.Kill(true)
                browser.WaitForExit()
                failtest "Chromium timed out"
            Expect.equal browser.ExitCode 0 errors.Result
            let matched=Regex.Match(output.Result,"<pre id=\"picker-results\">([^<]+)</pre>")
            Expect.isTrue matched.Success $"Incomplete browser result: {output.Result}\n{errors.Result}"
            use document=JsonDocument.Parse(WebUtility.HtmlDecode matched.Groups.[1].Value)
            let root=document.RootElement
            let errors = root.GetProperty("errors").EnumerateArray() |> Seq.map _.GetString() |> Seq.toList
            Expect.isEmpty errors (String.concat "\n" errors)
            Expect.equal (root.GetProperty("completed").GetInt32()) rows.Length "All picker lifecycles completed"
            let _,survivor,_,_=rows.[0]
            for packet in root.GetProperty("packets").EnumerateArray() do
                match packet.TryGetProperty "args" with
                | true,args ->
                    let sender,name=packet.GetProperty("sender").GetString(),packet.GetProperty("name").GetString()
                    let _,row,color,mouse=rows |> Array.find (fun (_,r,_,_) -> r.id=sender || r.root=sender)
                    let handler=if name="data-event" then color else mouse
                    let values=handler.invoke Guid.Empty sender (args.EnumerateArray() |> Seq.map _.GetString() |> Seq.toList) |> Seq.toList
                    if name="data-event" then
                        Expect.equal values [Color(C4b(32uy,64uy,128uy,128uy))] "Normal palette selection preserves alpha conversion"
                        Expect.isTrue (sender=survivor.id || row.mode="open") "Only living picker callbacks"
                    else Expect.equal values [Position(V2d(71.0,33.0),V2d(200.0,120.0))] "Native parent-relative bubbling geometry"
                | _ -> ()
            root.Clone()
        finally Directory.Delete(directory,true)

    [<Tests>]
    let tests =
        let runs=[lazy(runBrowser "spectrum.js");lazy(runBrowser "spectrum-min.js")]
        testList "Primitives.ColorPicker.Teardown" [
            for group in ["lifecycle";"plugin";"drag";"compatibility"] do
                testCase $"{group} preserves ownership for both Spectrum assets" (fun () ->
                    let failures = [
                        for index,run in List.indexed runs do
                            for error in run.Value.GetProperty(group).EnumerateArray() do
                                yield $"asset {index}: {error.GetString()}" ]
                    Expect.isEmpty failures (String.concat "\n" failures))
        ]
