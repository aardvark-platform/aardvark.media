namespace Aardvark.Cef.OffScreen

open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open CefSharp
open CefSharp.OffScreen
open System
open System.Runtime.InteropServices

type AardvarkCefBrowser private (settings: BrowserSettings, requestContext: IRequestContext, callback: Action<IBrowser>) =
    inherit ChromiumWebBrowser(
        address = "about:blank",
        browserSettings = settings,
        requestContext = requestContext,
        onAfterBrowserCreated = callback,
        useLegacyRenderHandler = false,
        automaticallyCreateBrowser = true
    )

    let lockObj = obj()

    let focus = ref false
    let flags = ref CefEventFlags.None
    let mutable host : IBrowserHost = null
    let mutable mouse = Unchecked.defaultof<BrowserMouse>
    let mutable keyboard = Unchecked.defaultof<BrowserKeyboard>
    let mutable renderHandler : AardvarkRenderHandler = null

    member private this.InvokeRenderHandler(action: AardvarkRenderHandler -> 'T) =
        lock lockObj (fun _ ->
            if this.IsDisposed && isNull renderHandler then raise <| ObjectDisposedException("AardvarkCefBrowser")
            action renderHandler
        )

    member private this.Initialize(browser: IBrowser, runtime: IRuntime, size: aval<V2i>, mipMap: bool) =
        host <- browser.GetHost()
        mouse <- BrowserMouse(host, focus, flags)
        keyboard <- BrowserKeyboard(host, focus, flags)
        renderHandler <- new AardvarkRenderHandler(host, runtime, size, mipMap)
        this.RenderHandler <- renderHandler

    static member internal Create(runtime: IRuntime, size: aval<V2i>, mipMap: bool, settings: BrowserSettings, requestContext: IRequestContext) =
        let defaultSettings =
            if isNull settings then
                new BrowserSettings(WindowlessFrameRate = 60)
            else
                null
        try
            let webBrowser, browser =
                let browser = MVar.empty()
                let webBrowser = new AardvarkCefBrowser(settings ||? defaultSettings, requestContext, MVar.put browser)
                webBrowser, browser.Take()

            webBrowser.Initialize(browser, runtime, size, mipMap)
            webBrowser
        finally
            defaultSettings.TryDispose() |> ignore

    member _.Mouse = mouse :> EventMouse

    member _.Keyboard = keyboard :> EventKeyboard

    [<CLIEvent>]
    member this.CursorChanged = this.InvokeRenderHandler _.CursorChanged

    member this.Texture = this.InvokeRenderHandler _.Texture

    member this.Version = this.InvokeRenderHandler _.Version

    member this.Size = this.InvokeRenderHandler _.Size

    member this.GetPixelValue(pixel: V2i) = this.InvokeRenderHandler _.GetPixelValue(pixel)

    member this.SetFocus (value: bool) =
        lock lockObj (fun () ->
            if notNull host then
                if focus.Value <> value then
                    focus.Value <- value
                    host.SendFocusEvent value
        )

    override this.Dispose(disposing) =
        if disposing then
            lock lockObj (fun _ ->
                if notNull renderHandler then
                    this.RenderHandler <- null
                    renderHandler.Dispose()
                    renderHandler <- null

                    host <- null
            )

        base.Dispose(disposing)