namespace Aardvark.Cef

open System.Collections.Concurrent
open Xilium.CefGlue

type AardvarkRenderProcessHandler() =
    inherit CefRenderProcessHandler()

    let aardvarks = ConcurrentDictionary<int, AardvarkIO>()

    override x.OnContextCreated(browser : CefBrowser, frame : CefFrame, ctx : CefV8Context) =
        base.OnContextCreated(browser, frame, ctx)

        let aardvark = 
            aardvarks.GetOrAdd(browser.Identifier, fun id -> AardvarkIO(browser))

        ctx.Use (fun () ->
            use scope = ctx.GetGlobal()
            use glob = scope.GetValue("document")
            use target = CefV8Value.CreateObject()
            glob.SetValue("aardvark", target, CefV8PropertyAttribute.DontDelete) 
                |> check "could not set global aardvark-value"


            for name in aardvark.FunctionNames do
                use f = CefV8Value.CreateFunction(name, aardvark)
                target.SetValue(name, f, CefV8PropertyAttribute.DontDelete) 
                    |> check "could not attach function to aardvark-value"
        )


    override x.OnProcessMessageReceived(browser, source, msg) =
        match IPC.tryReadProcessMessage<Response> msg with
            | Some response ->
                match aardvarks.TryGetValue browser.Identifier with
                    | (true, aardvark) ->
                        aardvark.got(response)
                    | _ ->
                        true
            | _ ->
                base.OnProcessMessageReceived(browser, source, msg)
