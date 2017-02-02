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
    | Css of string
    | Javascript of string
    | Html of string
    | Binary of byte[]
    | Error

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
            | Error ->
                "error", [||]
            | Css content ->
                "text/css", System.Text.Encoding.UTF8.GetBytes(content)
            | Javascript content ->
                "text/javascript", System.Text.Encoding.UTF8.GetBytes(content)
            | Html content ->
                "text/html", System.Text.Encoding.UTF8.GetBytes(content)
            | Binary content ->
                "application/octet-stream", content

    let mutable offset = 0L
    let mutable remaining = content.LongLength

    override x.ProcessRequest(request : CefRequest, callback : CefCallback) =
        if mime = "error" then
            false
        else
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
    static let queryRx = System.Text.RegularExpressions.Regex @"(\?|\&)(?<name>[^\=]+)\=(?<value>[^\&]*)"

    static let parseQuery (q : string) =
        let mutable res = Map.empty
        for m in queryRx.Matches q do
            let name = m.Groups.["name"].Value
            let value = m.Groups.["value"].Value
            res <- Map.add name value res
        res


    let mutable bf = Unchecked.defaultof<_>
    let ownBrowser = System.Threading.Tasks.TaskCompletionSource<CefBrowser * CefFrame>()

    let pages = System.Collections.Generic.Dictionary<string, Map<string, string> -> Content>()

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
            | (true, c) -> pageContent <- c (parseQuery url.Query); true
            | _ -> false

    member x.Item
        with set (url : string) (content : Map<string, string> -> Content) = 
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

    member x.Start(js : string) =
        async {
            let! (browser, frame) = ownBrowser.Task |> Async.AwaitTask
            browser.Send(CefProcessId.Renderer, IPC.Execute(IPC.MessageToken.Null, js))
        } |> Async.Start

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

    let sw = System.Diagnostics.Stopwatch()
    let mutable counter = 0
    do sw.Start()

    let renderResult =
        Mod.custom (fun self ->
            let fbo = framebuffer.GetValue self
            clearTask.Run(self, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
            renderTask.Run(self, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

            let color = colorTexture.GetValue(self)
            let image = colorImage.GetValue(self)
            runtime.Download(unbox color, 0, 0, image)

            if counter >= 100 then
                let fps = float counter / sw.Elapsed.TotalSeconds
                printfn "%.3f fps" fps
                sw.Restart()
                counter <- 0

            counter <- counter + 1
            image
        )

    let cb = 
        renderResult.AddMarkingCallback(fun () ->
            parent.Start (sprintf "render('%s');" id)
        )

    let binaryData (query : Map<string, string>) =
        match Map.tryFind "w" query, Map.tryFind "h" query with
            | Some w, Some h ->
                match Int32.TryParse w, Int32.TryParse h with
                    | (true, w), (true, h) ->
                        let size = V2i(w,h)
                        if size <> currentSize.Value then
                            transact (fun () -> currentSize.Value <- size)

                        let image = renderResult.GetValue()

                        transact (fun () -> 
                            time.MarkOutdated()
                        )

                        Binary image.Volume.Data
                    | _ ->
                        Error
            | _ ->
                Error
                        

    let url = sprintf "http://aardvark.local/render/%s" id

    do parent.[url] <- binaryData

    member x.Background
        with get() = backgroundColor.Value
        and set v = transact (fun () -> backgroundColor.Value <- v)

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

    //renderControl.Background <- C4f(0.0f, 0.0f, 0.0f, 0.0f)
    renderControl.RenderTask <- app.Runtime.CompileRender(renderControl.FramebufferSignature, sg)


    ctrl.StartUrl <- "http://aardvark.local"

    let style (u : Map<string, string>) =
        Css """

        body {
            margin: 0px;
            padding: 0px;
            border: 0px;
        }

        div.aardvark {
            
        }

        """

    let boot (u : Map<string, string>) =
        Javascript """

       
            function render(id) {
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();

                $canvas = $('#'+ id + ' canvas');
	            var canvas = $canvas.get(0);
	            var ctx = canvas.getContext('2d');
                
                if(canvas.width != w || canvas.height != h) {
                    canvas.width = w;
                    canvas.height = h;
                }

	            var oReq = new XMLHttpRequest();
	            oReq.open("GET", "http://aardvark.local/render/" + id + "?w=" + w + "&h=" + h, true);
	            oReq.responseType = "arraybuffer";

	            oReq.onload = 
		            function (oEvent) {
			            var arrayBuffer = oReq.response;
			            if (arrayBuffer) {
				            var byteArray = new Uint8Array(arrayBuffer);
				            var imageData = ctx.createImageData(w, h);
				            imageData.data.set(byteArray);
				            ctx.putImageData(imageData, 0, 0);
			            }
		            };

	            oReq.send(null);
            }

            function init(id) {
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();
                $div.append($('<canvas/>'));

                render(id);
            }


            $(document).ready(function() {
	            $('div.aardvark').each(function() {
                    init($(this).get(0).id);
	            });
            });
        """

    let mainPage (u : Map<string, string>) =
        Html """
            <html>
                <head>
                    <title>BLA</title>
                    <link rel="stylesheet" type="text/css" href="http://aardvark.local/style.css">
                    <script src="https://code.jquery.com/jquery-3.1.1.min.js"></script>
                    <script src="http://aardvark.local/boot.js"></script>
                </head>
                <body>
                    <button onclick="aardvark.processEvent('button', 'onclick')">Click Me</button>
                    <div class='aardvark' id='yeah' style="height: 100%; width: 100%" />

                </body>
            </html>
        """

    ctrl.["http://aardvark.local"] <- mainPage
    ctrl.["http://aardvark.local/boot.js"] <- boot
    ctrl.["http://aardvark.local/style.css"] <- style

    ctrl.Events.Add (fun e ->
        printfn "{ sender = %A; name = %A }" e.sender e.name
    )

    use form = new Form()
    form.Width <- 1024
    form.Height <- 768
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl

    Application.Run form

    Chromium.shutdown()

    0 // return an integer exit code
