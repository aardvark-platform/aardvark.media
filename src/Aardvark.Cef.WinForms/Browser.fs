namespace Aardvark.Cef.WinForms

open CefSharp
open CefSharp.Web
open CefSharp.WinForms
open System

type AardvarkCefBrowser =
    inherit ChromiumWebBrowser

    static let disableZoomJs = @"
        window.addEventListener('wheel', function(e) {
            if (e.ctrlKey) {
                e.preventDefault();
            }
        }, { passive: false });"

    private new (address: string, requestContext: IRequestContext) =
        { inherit ChromiumWebBrowser(address, requestContext) }

    private new (html: HtmlString, requestContext: IRequestContext) =
        { inherit ChromiumWebBrowser(html, requestContext) }

    member private this.Initialize() =
        AardvarkRenderProcessMessageHandler.Setup this

        this.FrameLoadEnd.Add (fun args ->
            if args.Frame.IsMain && args.Frame.IsValid then
                args.Frame.ExecuteJavaScriptAsync(disableZoomJs)
        )

    static member internal Create(address: string, requestContext: IRequestContext) =
        let browser = new AardvarkCefBrowser(address, requestContext)
        browser.Initialize()
        browser

    static member internal Create(html: HtmlString, requestContext: IRequestContext) =
        let browser = new AardvarkCefBrowser(html, requestContext)
        browser.Initialize()
        browser

    member this.OpenDevTools() =
        if this.IsBrowserInitialized then
            this.ShowDevTools()
        else
            this.IsBrowserInitializedChanged.Add (fun _ ->
                this.BeginInvoke(Action this.ShowDevTools) |> ignore
            )

    member this.CloseDevTools() =
        if this.IsBrowserInitialized then
            this.CloseDevTools()