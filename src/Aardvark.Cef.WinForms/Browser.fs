namespace Aardvark.Cef.WinForms

open CefSharp
open CefSharp.Web
open CefSharp.WinForms
open System
open System.Runtime.InteropServices

type AardvarkCefBrowser =
    inherit ChromiumWebBrowser

    private new (address: string, requestContext: IRequestContext) =
        { inherit ChromiumWebBrowser(address, requestContext) }

    private new (html: HtmlString, requestContext: IRequestContext) =
        { inherit ChromiumWebBrowser(html, requestContext) }

    static member Create(address: string, [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        let browser = new AardvarkCefBrowser(address, requestContext)
        AardvarkRenderProcessMessageHandler.Setup browser
        browser

    static member Create(html: HtmlString, [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        let browser = new AardvarkCefBrowser(html, requestContext)
        AardvarkRenderProcessMessageHandler.Setup browser
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