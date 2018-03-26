namespace Aardvark.Cef

open Xilium.CefGlue

type AardvarkCefApp() =
    inherit CefApp()

    let handler = lazy (AardvarkRenderProcessHandler())

    override x.GetRenderProcessHandler() =
        handler.Value :> CefRenderProcessHandler


