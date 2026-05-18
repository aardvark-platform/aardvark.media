namespace Aardvark.Cef.WinForms

open CefSharp
open CefSharp.WinForms
open System.Diagnostics

#if NETFRAMEWORK
[<AutoOpen>]
module internal JavascriptObjectRepositoryExtensions =
    type IJavascriptObjectRepository with
        member this.Register(name: string, objectToBind: obj) =
            this.Register(name, objectToBind, true)
#endif

type internal AardvarkRenderProcessMessageHandler private () =
    static member Setup(browser: ChromiumWebBrowser) =
        browser.RenderProcessMessageHandler <- AardvarkRenderProcessMessageHandler()
        browser.JavascriptObjectRepository.Register("aardvarkDialogHandler", AardvarkDialogHandler browser)

    member _.OnContextCreated(_: IWebBrowser, _: IBrowser, frame: IFrame) =
        frame.ExecuteJavaScriptAsync($"(function() {{ {AardvarkDialogHandler.Javascript} }})();")

    member _.OnUncaughtException(_: IWebBrowser, _: IBrowser, _: IFrame, exn: JavascriptException) =
        Debugger.Break()

    interface IRenderProcessMessageHandler with
        member this.OnContextCreated(webBrowser, browser, frame) = this.OnContextCreated(webBrowser, browser, frame)
        member this.OnContextReleased(_, _, _) = ()
        member this.OnFocusedNodeChanged(_, _, _, _) = ()
        member this.OnUncaughtException(webBrowser, browser, frame, exn) = this.OnUncaughtException(webBrowser, browser, frame, exn)