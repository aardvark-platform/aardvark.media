namespace Aardvark.UI

open System
open Xilium.CefGlue
open Xilium.CefGlue.Platform.Windows
open Xilium.CefGlue.WindowsForms
open Aardvark.Base
open System.Reflection
open Aardvark.Cef
open System.Windows.Forms
open System.IO
open System.Threading


module Chromium =

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
            match IPC.tryReadProcessMessage<Command> msg with
                | Some command ->
                    match command with
                        | OpenDialog(id, config) ->
                            let showDialog = 
                                match config.mode with
                                    | OpenDialogMode.Folder ->
                                        fun () ->
                                            let dialog = 
                                                new FolderBrowserDialog(
                                                    Description = config.title,
                                                    SelectedPath = getInitialPath config.startPath
                                                )
                                            let res = dialog.ShowDialog()
                                            match res with  
                                                | DialogResult.OK -> 
                                                    let path = dialog.SelectedPath
                                                    use msg = IPC.toProcessMessage (Response.Ok(id, [path]))
                                                    sourceBrowser.SendProcessMessage(CefProcessId.Renderer, msg) |> ignore
                                                | _ -> 
                                                    use msg = IPC.toProcessMessage (Response.Abort id)
                                                    sourceBrowser.SendProcessMessage(CefProcessId.Renderer, msg) |> ignore
                                    | OpenDialogMode.File -> 
                                        fun () -> 
                                            let apartmentState = Thread.CurrentThread.GetApartmentState()
                                            if apartmentState <> ApartmentState.STA then
                                                let err = "cannot open FileDialog on MTA thread"

                                                Log.error "[CEF] %s" err
                                                use msg = IPC.toProcessMessage (Response.Error(id, err))
                                                sourceBrowser.SendProcessMessage(CefProcessId.Renderer, msg) |> ignore
                                            else
                                                let dialog = 
                                                    new OpenFileDialog(
                                                        Title = config.title,
                                                        Multiselect = config.allowMultiple,
                                                        InitialDirectory = getInitialPath config.startPath
                                                    )


                                                if config.filters.Length > 0 then
                                                    dialog.Filter <- "File|" + String.concat ";" config.filters
                                                    //dialog.FilterIndex <- config.activeFilter

                                                let res = dialog.ShowDialog()


                                                match res with
                                                    | DialogResult.OK -> 
                                                        let files = dialog.FileNames
                                                        let path = files |> Seq.truncate 1 |> Seq.map Path.GetDirectoryName |> Seq.tryHead
                                                        match path with
                                                            | Some p -> setPath p
                                                            | None -> ()

                                                        let files = files |> Array.map PathUtils.toUnixStyle
                                                        use msg = IPC.toProcessMessage (Response.Ok(id, Array.toList files))
                                                        sourceBrowser.SendProcessMessage(CefProcessId.Renderer, msg) |> ignore
                                                    | _ -> 
                                                        use msg = IPC.toProcessMessage (Response.Abort id)
                                                        sourceBrowser.SendProcessMessage(CefProcessId.Renderer, msg) |> ignore
                                    | _ -> 
                                        Log.warn "unknown openDialogMode"
                                        fun () -> ()


                            browser.BeginInvoke(Action(showDialog)) |> ignore
                            true
                | None ->
                    base.OnProcessMessageReceived(sourceBrowser, source, msg)

    let init argv = Cef.init argv
 
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
        new Chromium.MyCefClient(x) :> CefWebClient

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