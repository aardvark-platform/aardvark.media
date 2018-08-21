#nowarn "044"

open Aardvark.Cef
open Aardvark.Cef.Internal
open System.Windows.Forms
open Xilium.CefGlue.WindowsForms
open Xilium.CefGlue

open System
open System.IO
open System.Reflection
open Aardvark.Base
open Aardvark.Application.WinForms
open Suave
open Aardvark.UI
open Aardvark.Rendering.Vulkan

type MyCefClient(browser : CefWebBrowser) =
    inherit CefWebClient(browser)

    let lastPathFile = 
        let programName = Assembly.GetEntryAssembly().FullName
        Path.Combine(Path.GetTempPath(), programName + ".path")

    let getInitialPath (def : string) =
        if File.Exists lastPathFile then
            File.ReadAllText lastPathFile
        else
            def

    let setPath (path : string) =
        File.WriteAllText(lastPathFile, path)


    override x.OnProcessMessageReceived(sourceBrowser, source, msg) =
        base.OnProcessMessageReceived(sourceBrowser, source, msg)

type AardvarkCefBrowser() =
    inherit CefWebBrowser()

    do base.BrowserSettings <- 
        CefBrowserSettings(
            LocalStorage = CefState.Enabled, 
            ApplicationCache = CefState.Enabled,
            JavaScriptAccessClipboard = CefState.Enabled,
            JavaScriptCloseWindows = CefState.Enabled,
            JavaScriptDomPaste = CefState.Enabled,
            TextAreaResize = CefState.Enabled,
            UniversalAccessFromFileUrls = CefState.Enabled,
            WebGL = CefState.Enabled
        )
    let mutable devTools = false
    
    let ownBrowser = System.Threading.Tasks.TaskCompletionSource<CefBrowser>()

    let showDevTools (x : AardvarkCefBrowser) =
        let form = x.FindForm()
        let host = x.Browser.GetHost()
        let parent = host.GetWindowHandle()
        let wi = CefWindowInfo.Create();
        //wi.SetAsChild(parent, CefRectangle(0, x.ClientSize.Height - 200, x.ClientSize.Width, 200))
        wi.SetAsPopup(parent, "Developer Tools");
        wi.Width <- 500
        wi.Height <- form.Height
        wi.X <- form.DesktopLocation.X + form.Width
        wi.Y <- form.DesktopLocation.Y
        
        //wi.Style <- WindowStyle.WS_POPUP
        host.ShowDevTools(wi, new DevToolsWebClient(x), new CefBrowserSettings(), CefPoint(-1, -1));

    let closeDevTools (x : AardvarkCefBrowser) =
        let host = x.Browser.GetHost()
        host.CloseDevTools()

    override x.CreateWebClient() =
        new MyCefClient(x) :> CefWebClient

    member x.ShowDevTools() =
        if not devTools then
            devTools <- true

            let hasBrowser =
                try not (isNull x.Browser)
                with _ -> false

            if hasBrowser then
                showDevTools x
            else
                x.BrowserCreated.Add (fun _ -> 
                    if devTools then
                        showDevTools x
                )

and private DevToolsWebClient(parent : AardvarkCefBrowser) =
    inherit CefClient()

module Shared =
    open Aardvark.Base.Incremental
    let sg<'a> : ISg<'a> = 
        [for x in -10.0 .. 1.0 .. 10.0 do
            for y in -10.0 .. 1.0 .. 10.0 do
                for z in -10.0 .. 1.0 .. 10.0 do
                    //yield Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit) |> Sg.scale 0.5 |> Sg.translate x y z
                    yield Sg.sphere (10) (Mod.constant C4b.Green) (Mod.constant 0.7) |> Sg.translate x y z
        ] |> Sg.ofSeq

module TestApp =

    open Aardvark.UI
    open Aardvark.UI.Primitives

    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Model

    type Message = Camera of CameraController.Message

    let update (model : Model) (msg : Message) =
        match msg with
           | Camera m -> { model with cameraState = CameraController.update model.cameraState m}

    let viewScene (model : MModel) =
        Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }

    let view (model : MModel) =

        let renderControl =
            CameraController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                        (AttributeMap.ofList [ style "width: 100%; grid-row: 2"; 
                                               attribute "showFPS" "true";       // optional, default is false
                                               //attribute "showLoader" "false"  // optional, default is true
                                               attribute "data-renderalways" "1" // optional, default is incremental rendering
                                               attribute "useMapping" "true"
                                             ]) 
                        (viewScene model)


        div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
            div [style "grid-row: 1"] [
                text "Hello 3D"
                br []
            ]
            renderControl
            br []
            text "use first person shooter WASD + mouse controls to control the 3d scene"
        ]

    let threads (model : Model) = 
        CameraController.threads model.cameraState |> ThreadPool.map Camera


    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                   cameraState = CameraController.initial
                }
            update = update 
            view = view
        }


 module TestApp2 =

    module PerClient = 
        open Aardvark.UI
        open Aardvark.UI.Primitives

        open Aardvark.Base
        open Aardvark.Base.Incremental
        open Aardvark.Base.Rendering
        open Model

        type Message = Camera of FreeFlyController.Message | CenterScene

        let update (model : Model) (msg : Message) =
            match msg with
               | Camera m -> { model with cameraState = FreeFlyController.update model.cameraState m}
               | CenterScene -> { model with cameraState = FreeFlyController.initial }

        let viewScene (model : MModel) =
            //Shared.sg
            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
             |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }

        let view (model : MModel) =

            let renderControl =
               FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                            (AttributeMap.ofList [ style "width: 100%; grid-row: 2"; 
                                                   attribute "showFPS" "true";       // optional, default is false
                                                   //attribute "showLoader" "false"  // optional, default is true
                                                   //attribute "data-renderalways" "1" // optional, default is incremental rendering
                                                   attribute "useMapping" "true"
                                                 ]) 
                            (viewScene model)


            div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
                div [style "grid-row: 1"] [
                    text "Hello 3D"
                    br []
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
                ]
                renderControl
                br []
                text "use first person shooter WASD + mouse controls to control the 3d scene"
            ]

        let threads (model : Model) = 
            ThreadPool.empty //FreeFlyController.threads model.cameraState |> ThreadPool.map Camera


        let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
            {
                unpersist = Unpersist.instance     
                threads = threads 
                initial = 
                    { 
                       cameraState = FreeFlyController.initial
                    }
                update = update 
                view = view
            }

    module Server =
        
        open Aardvark.UI
        open Aardvark.UI.Primitives

        open Aardvark.Base
        open Aardvark.Base.Incremental
        open Aardvark.Base.Rendering
        open Model

        type Message = Nop

        let update (model : ServerModel) (msg : Message) =
            match msg with
               | Nop -> model

        let view (model : MServerModel) =

            body [] [
                subApp' (fun _ _ -> Seq.empty) (fun _ _ -> Seq.empty) [] PerClient.app
            ]

        let threads (model : ServerModel) = 
            ThreadPool.empty


        let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
            {
                unpersist = Unpersist.instance     
                threads = threads 
                initial = 
                    { 
                       value = 0
                    }
                update = update 
                view = view
            }

[<EntryPoint>]
let main argv = 
    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init ()

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    let instance = TestApp.app |> App.start
    //let instance = TestApp2.Server.app |> App.start

    // use can use whatever suave server to start you mutable app. 
    // startServerLocalhost is one of the convinience functions which sets up 
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer. 
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.Service/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    WebPart.startServerLocalhost 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ]  


    use form = new Form()
    form.Width <- 800
    form.Height <- 600

    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"
    ctrl.ShowDevTools()
    form.Text <- "Examples"

    Application.Run form

    Chromium.shutdown()
    0 
