open Aardvark.Cef
open Aardvark.Cef.Internal
open System.Windows.Forms
open Xilium.CefGlue.WindowsForms
open Xilium.CefGlue

open System
open System.IO
open System.Reflection

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

[<EntryPoint>]
let main argv = 
    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init ()


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
