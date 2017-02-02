// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

#nowarn "9"

open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Windows.Forms
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Cef
open Xilium.CefGlue
open Xilium.CefGlue.WindowsForms

open Aardvark.Cef.Internal.CefExtensions
module IPC = Aardvark.Cef.Internal.IPC

type Content =
    | Html of string
    | Binary of byte[]

type LoadHandler(parent : BrowserControl) =
    inherit CefLoadHandler()

    override x.OnLoadStart(browser : CefBrowser, frame : CefFrame, transitionType : CefTransitionType) =
        parent.SetBrowser(browser, frame)
        base.OnLoadStart(browser, frame, transitionType)

    override x.OnLoadEnd(browser : CefBrowser, frame : CefFrame, status : int) =
        parent.LoadFinished()
        base.OnLoadEnd(browser, frame, status)

    override x.OnLoadError(browser : CefBrowser, frame : CefFrame, errorCode : CefErrorCode, errorText : string, failedUrl : string) =
        parent.LoadFaulted(errorText, failedUrl)
        base.OnLoadError(browser, frame, errorCode, errorText, failedUrl)

and ResourceHandler(parent : BrowserControl, content : Content) =
    inherit CefResourceHandler()

    let mime, content = 
        match content with
            | Html content ->
                "text/html", Array.append (System.Text.Encoding.UTF8.GetBytes(content)) [|0uy|]
            | Binary content ->
                "application/octet-stream", content

    let mutable offset = 0L
    let mutable remaining = content.LongLength

    override x.ProcessRequest(request : CefRequest, callback : CefCallback) =
        callback.Continue()
        true

    override x.GetResponseHeaders(response : CefResponse, response_length : byref<int64>, redirectUrl : byref<string>) =
        response_length <- content.LongLength
        redirectUrl <- null
        response.MimeType <- mime
        response.Status <- 200
        response.StatusText <- "OK"
        response.Error <- CefErrorCode.None

    override x.ReadResponse(data_out : Stream, bytes_to_read : int, bytes_read : byref<int>, callback : CefCallback) =
        if remaining > 0L then
            let actual = min (int64 bytes_to_read) remaining
            data_out.Write(content, int offset, int actual)

            offset <- offset + actual
            remaining <- remaining - actual
            bytes_read <- int actual
            true
        else
            bytes_read <- 0
            false

    override x.CanGetCookie(c : CefCookie) = true
    override x.CanSetCookie(c : CefCookie) = true
    override x.Cancel() = ()

and RequestHandler(parent : BrowserControl) =
    inherit CefRequestHandler()

    override x.GetResourceHandler(browser : CefBrowser, frame : CefFrame, request : CefRequest) =
        match parent.TryGetPage request.Url with
            | (true, page) -> 
                ResourceHandler(parent, page) :> CefResourceHandler
            | _ -> 
                base.GetResourceHandler(browser, frame, request)
        
and BrowserClient(parent : BrowserControl) =
    inherit CefWebClient(parent)

    let loadHandler = LoadHandler(parent)
    let requestHandler = RequestHandler(parent)
    override x.OnProcessMessageReceived(source, proc, msg) =
        parent.OnProcessMessageReceived(source, proc, msg)
        true

    override x.GetLoadHandler() =
        loadHandler :> CefLoadHandler

    override x.GetRequestHandler() =
        requestHandler :> CefRequestHandler

and BrowserControl() as this =
    inherit CefWebBrowser()

    let mutable bf = Unchecked.defaultof<_>
    let ownBrowser = System.Threading.Tasks.TaskCompletionSource<CefBrowser * CefFrame>()

    let pages = System.Collections.Generic.Dictionary<string, System.Uri -> Content>()

    let pending = System.Collections.Concurrent.ConcurrentDictionary<IPC.MessageToken, System.Threading.Tasks.TaskCompletionSource<IPC.Reply>>()
    let events = Event<_>()

    member x.RemovePage(url : string) =
        let url = System.Uri(url)
        let path = url.GetLeftPart(System.UriPartial.Path)
        pages.Remove path

    member x.TryGetPage(url : string, [<Out>] pageContent : byref<Content>) =
        let url = System.Uri(url)
        let path = url.GetLeftPart(System.UriPartial.Path)
        match pages.TryGetValue path with
            | (true, c) -> pageContent <- c url; true
            | _ -> false

    member x.Item
        with set (url : string) (content : System.Uri -> Content) = 
            let url = System.Uri(url)
            let path = url.GetLeftPart(System.UriPartial.Path)
            pages.[path] <- content

    member x.SetBrowser(browser : CefBrowser, frame : CefFrame) =

        printfn "SetBrowser"
        bf <- (browser, frame)

    member x.LoadFinished() =
        printfn "LoadFinished"
        ownBrowser.SetResult bf

    member x.LoadFaulted(err : string, url : string) =
        printfn "LoadFaulted"

    member x.Run(js : string) =
        async {
            let! (browser, frame) = ownBrowser.Task |> Async.AwaitTask
            printfn "running"
            let token = IPC.MessageToken.New
            let tcs = System.Threading.Tasks.TaskCompletionSource()
            pending.[token] <- tcs

            browser.Send(CefProcessId.Renderer, IPC.Execute(token, js))

            let! res = tcs.Task |> Async.AwaitTask
            return res
        }

    [<CLIEvent>]
    member x.Events = events.Publish

    member x.OnProcessMessageReceived(source : CefBrowser, proc : CefProcessId, msg : CefProcessMessage) =
        match IPC.tryGet<Aardvark.Cef.Internal.Event> msg with
            | Some evt ->
                events.Trigger evt
            | None -> 
                match IPC.tryGet<IPC.Reply> msg with
                    | Some reply ->
                        match pending.TryRemove reply.Token with
                            | (true, tcs) -> tcs.SetResult reply
                            | _ -> ()

                    | _ ->
                        ()

    override x.CreateWebClient() =
        BrowserClient(this) :> CefWebClient

open System
open System.IO
open System.Diagnostics
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Application
open Aardvark.Application.WinForms


type CefRenderControl(runtime : IRuntime, parent : BrowserControl, id : string) =

    static let queryRx = System.Text.RegularExpressions.Regex @"(\?|\&)(?<name>[^\=]+)\=(?<value>[^\&]*)"

    let backgroundColor = Mod.init C4f.Black
    let currentSize = Mod.init V2i.II
    let signature =
        runtime.CreateFramebufferSignature(
            1, [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]
        )

    let framebuffer = runtime.CreateFramebuffer(signature, Set.ofList [DefaultSemantic.Colors], currentSize)
    do framebuffer.Acquire()

    let colorTexture = framebuffer.GetOutputTexture(DefaultSemantic.Colors)
    let colorImage = currentSize |> Mod.map (fun s -> PixImage<byte>(Col.Format.RGBA, s))
    let clearTask = runtime.CompileClear(signature, backgroundColor, Mod.constant 1.0)
    let mutable renderTask = RenderTask.empty
    let time = Mod.custom (fun _ -> DateTime.Now)

    let render (size : V2i) =
        if size <> currentSize.Value then
            transact (fun () -> currentSize.Value <- size)

        let fbo = framebuffer.GetValue()
        clearTask.Run(RenderToken.Empty, fbo)
        renderTask.Run(RenderToken.Empty, fbo)

        let color = colorTexture.GetValue()
        let image = colorImage.GetValue()
        runtime.Download(unbox color, 0, 0, image)

        transact (fun () -> 
            time.MarkOutdated()
        )

        image

    let parseSize (url : System.Uri) =
        let mutable w = -1
        let mutable h = -1
        let query = System.Net.WebUtility.UrlDecode(url.Query)
        for m in queryRx.Matches query do
            let name = m.Groups.["name"].Value
            let value = m.Groups.["value"].Value

            match name with
                | "w" -> 
                    match Int32.TryParse value with
                        | (true, v) -> w <- v
                        | _ -> ()
                | "h" ->
                    match Int32.TryParse value with
                        | (true, v) -> h <- v
                        | _ -> ()
                | _ ->
                    ()
        if w <= 0 || h <= 0 then
            failwith ""

        V2i(w,h)

    let binaryData (url : System.Uri) =
        let size = parseSize url
        let image = render size
        Binary image.Volume.Data

    let url = sprintf "http://aardvark.local/render/%s" id

    do parent.[url] <- binaryData

    member x.Dispose() =
        parent.RemovePage url |> ignore
        framebuffer.Release()
        clearTask.Dispose()
        renderTask.Dispose()
        renderTask <- RenderTask.empty

    member x.RenderTask
        with get() = renderTask
        and set v = 
            renderTask.Dispose()
            renderTask <- v

    member x.Sizes = currentSize :> IMod<_>

    member x.FramebufferSignature = signature

    member x.Time = time

    member x.Samples = 1
    
    member x.Runtime = runtime

    interface IRenderTarget with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Runtime = x.Runtime
        member x.Sizes = x.Sizes
        member x.Samples = x.Samples
        member x.Time = x.Time

    interface IRenderControl with
        member x.Keyboard = failwith ""
        member x.Mouse = failwith ""

    interface IDisposable with
        member x.Dispose() = x.Dispose()





//
//type ChromeApplication(runtime : IRuntime) =
//    
open Aardvark.Base.Rendering
open Aardvark.SceneGraph



[<EntryPoint>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    Chromium.init' false

    use ctrl = new BrowserControl()

    let app = new OpenGlApplication()
    let renderControl = new CefRenderControl(app.Runtime, ctrl, "yeah")


    let view = renderControl.Time |> Mod.map (fun t -> CameraView.lookAt (M44d.RotationZ(0.5 * float t.Ticks / float TimeSpan.TicksPerSecond).TransformPos(V3d.III * 6.0)) V3d.Zero V3d.OOI)
    let proj = renderControl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))



    let sg =
        Sg.box' C4b.Red (Box3d(-V3d.III, V3d.III))
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

    renderControl.RenderTask <- app.Runtime.CompileRender(renderControl.FramebufferSignature, sg)


    ctrl.StartUrl <- "http://aardvark.local"

    let mainPage (u : System.Uri) =
        Html """
            <html>
                <head>
                    <title>BLA</title>
                    <script>
                        function load() {
                            var canvas = document.getElementById('yeah');
                            var ctx = canvas.getContext('2d');

                            var w = canvas.width;
                            var h = canvas.height;

                            var oReq = new XMLHttpRequest();
                            oReq.open("GET", "http://aardvark.local/render/yeah?w=" + w + "&h=" + h, true);
                            oReq.responseType = "arraybuffer";

                            oReq.onload = function (oEvent) {
                                var arrayBuffer = oReq.response;
                                if (arrayBuffer) {
                                    var byteArray = new Uint8Array(arrayBuffer);
                                    var imageData = ctx.getImageData(0, 0, w, h);
                                    imageData.data.set(byteArray);
                                    ctx.putImageData(imageData, 0, 0);
                                }
                            };

                            oReq.send(null);
                        }
                    </script>
                </head>
                <body>
                    <h1 id='bla'>Hi There</h1>
                    <button onclick="load()">BlaBla</button>
                    <br>
                    <canvas id='yeah' width='640' height='480'></canvas>
                </body>
            </html>
        """

    ctrl.["http://aardvark.local"] <- mainPage


    use form = new Form()
    form.Width <- 1024
    form.Height <- 768
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl

    Application.Run form

    Chromium.shutdown()

    0 // return an integer exit code
