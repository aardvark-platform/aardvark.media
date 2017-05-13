namespace Aardvark.UI

open System
open Xilium.CefGlue
open Xilium.CefGlue.Platform.Windows
open Xilium.CefGlue.WindowsForms

open Aardvark.Base

module Chromium =
    open System.Reflection
    open System.Windows.Forms
    open System.Threading
    open System.Collections.Concurrent
    open System.Collections.Generic

    open Xilium.CefGlue.Wrapper
    open Xilium.CefGlue

    type CefResult =
        | Success of CefV8Value
        | NoRet
        | Error of string

    type CefV8Context with
        member x.Use (f : unit -> 'a) =
            let mutable entered = false
            try
                entered <- x.Enter()
                f()
            finally
                if entered then
                    let exited = x.Exit()
                    if not exited then 
                        failwith "[Cef] could not exit CefV8Context"

    [<AutoOpen>]
    module Patterns =

        let (|StringValue|_|) (v : CefV8Value) =
            if v.IsString then
                Some (v.GetStringValue())
            else
                None

        let (|IntValue|_|) (v : CefV8Value) =
            if v.IsInt then
                Some (v.GetIntValue())
            else
                None

        let (|UIntValue|_|) (v : CefV8Value) =
            if v.IsUInt then
                Some (v.GetUIntValue())
            else
                None

        let (|BoolValue|_|) (v : CefV8Value) =
            if v.IsBool then
                Some (v.GetBoolValue())
            else
                None

        let (|DateValue|_|) (v : CefV8Value) =
            if v.IsDate then
                Some (v.GetDateValue())
            else
                None

        let (|DoubleValue|_|) (v : CefV8Value) =
            if v.IsDouble then
                Some (v.GetDoubleValue())
            else
                None

        let (|NullValue|_|) (v : CefV8Value) =
            if v.IsNull then
                Some ()
            else
                None

        let (|UndefinedValue|_|) (v : CefV8Value) =
            if v.IsUndefined then
                Some ()
            else
                None

        let (|ObjectValue|_|) (v : CefV8Value) =
            if v.IsObject then
                let keys = v.GetKeys()
                let values = Seq.init keys.Length (fun i -> keys.[i], v.GetValue(keys.[i])) |> Map.ofSeq

                Some values
            else
                None
                
                
        let (|FunctionValue|_|) (v : CefV8Value) =
            if v.IsFunction then
                Some (fun args -> v.ExecuteFunction(v, args))
            else
                None

        let (|ArrayValue|_|) (v : CefV8Value) =
            if v.IsArray then
                let len = v.GetArrayLength()
                let arr = Array.init len (fun i -> v.GetValue i)
                Some arr
            else
                None


    type OpenDialogMode =
        | File = 0
        | Folder = 1

    type OpenDialogConfig =
        {
            mode            : OpenDialogMode
            title           : string
            startPath       : string
            filters         : string[]
            activeFilter    : int
            allowMultiple   : bool
        }


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module OpenDialogConfig =
        let empty =
            {
                mode = OpenDialogMode.File
                title = "Open File"
                startPath = Environment.GetFolderPath Environment.SpecialFolder.Desktop
                filters = [||]
                activeFilter = -1
                allowMultiple = false
            }

        let parse (value : CefV8Value) =
            let mutable res = empty

            match value with
                | ObjectValue map ->
                    match Map.tryFind "mode" map with
                        | Some (StringValue "file") -> res <- { res with mode = OpenDialogMode.File }
                        | Some (StringValue "folder") -> res <- { res with mode = OpenDialogMode.Folder }
                        | _ -> ()

                    match Map.tryFind "startPath" map with
                        | Some (StringValue path) -> res <- { res with startPath = path }
                        | _ -> ()

                    match Map.tryFind "title" map with
                        | Some (StringValue v) -> res <- { res with title = v }
                        | _ -> ()

                    match Map.tryFind "filters" map with
                        | Some (ArrayValue filters) ->
                            let filters = filters |> Array.choose (function StringValue v -> Some v | _ -> None)
                            res <- { res with filters = filters }

                        | _ ->
                            ()

                    match Map.tryFind "activeFilter" map with
                        | Some (IntValue v) -> res <- { res with activeFilter = v }
                        | Some (UIntValue v) -> res <- { res with activeFilter = int v }
                        | Some (DoubleValue v) -> res <- { res with activeFilter = int v }
                        | _ -> ()

                    match Map.tryFind "allowMultiple" map with
                        | Some (BoolValue v) -> res <- { res with allowMultiple = v }
                        | _ -> ()

                | _ ->
                    ()

            res

    type Command =
        | OpenDialog of int * OpenDialogConfig

    type Response =
        | Abort of int
        | Ok of int * list<string>

        member x.id =
            match x with
                | Abort i -> i
                | Ok(i,_) -> i

    module IPC =
        let pickler = MBrace.FsPickler.BinarySerializer()

        let toProcessMessage (a : 'a) =
            let message = CefProcessMessage.Create(typeof<'a>.Name)
            use v = CefBinaryValue.Create(pickler.Pickle a)
            message.Arguments.SetBinary(0, v) |> ignore
            message

        let tryReadProcessMessage<'a> (msg : CefProcessMessage) : Option<'a> =
            if msg.Name = typeof<'a>.Name then
                let bin = msg.Arguments.GetBinary(0)
                bin.ToArray() |> pickler.UnPickle|> Some
            else
                None

    type AardvarkIO(browser : CefBrowser, ctx : CefV8Context) as this =
        inherit CefV8Handler()

        let functions : Dictionary<string, CefV8Value[] -> CefResult> = 
            typeof<AardvarkIO>.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Seq.filter (fun mi -> mi.ReturnType = typeof<CefResult> && mi.GetParameters().Length = 1)
                |> Seq.map (fun mi -> mi.Name, FunctionReflection.buildFunction this mi)
                |> Dictionary.ofSeq

                
        let runner = ctx.GetTaskRunner()
        let reactions = ConcurrentDictionary<int, CefV8Value[] -> CefV8Value>()
        let mutable currentId = 0

        member x.FunctionNames = 
            functions.Keys

        override x.Execute(name, _, args, ret, exn) =
            match functions.TryGetValue(name) with
                | (true, f) ->
                    match f args with
                        | Success v -> 
                            ret <- v
                            true
                        | NoRet ->
                            true
                        | Error err ->
                            exn <- err
                            false
                | _ ->
                    exn <- sprintf "unknown function %s" name
                    false

        member x.got(response : Response) =
            match reactions.TryRemove response.id with
                | (true, f) ->
                    runner.PostTask {
                        new CefTask() with
                            member x.Execute() =
                                ctx.Use (fun () ->
                                    match response with
                                        | Ok(_,files) ->
                                            let files = List.toArray files
                                            use arr = CefV8Value.CreateArray(files.Length)
                                            for i in 0 .. files.Length - 1 do
                                                use file = CefV8Value.CreateString files.[i]
                                                arr.SetValue(i, file) |> ignore

                                            f [| arr |] |> ignore
                                        | Abort _ ->
                                            f [| |] |> ignore
                                )
                    }
                | _ ->
                    true

        member x.openFileDialog(args : CefV8Value[]) =
            
            let config =
                match args with
                    | [| FunctionValue f |] -> Some (OpenDialogConfig.empty, f)
                    | [| v; FunctionValue f |] -> Some(OpenDialogConfig.parse v, f)
                    | _ -> None

            match config with
                | Some (config, f) -> 
                    let id = Interlocked.Increment(&currentId)
                    reactions.[id] <- f
                    use msg = IPC.toProcessMessage (OpenDialog(id, config))
                    browser.SendProcessMessage(CefProcessId.Browser, msg) |> ignore

                    NoRet
                | _ ->
                    Error "no argument for openFileDialog"

    let inline check str v = if not v then failwithf "[CEF] %s" str
   

    type RenderProcessHandler() =
        inherit CefRenderProcessHandler()

        let aardvarks = ConcurrentDictionary<int, AardvarkIO>()

        override x.OnContextCreated(browser : CefBrowser, frame : CefFrame, ctx : CefV8Context) =
            base.OnContextCreated(browser, frame, ctx)

            ctx.Use (fun () ->
                use scope = ctx.GetGlobal()
                use glob = scope.GetValue("document")
                use target = CefV8Value.CreateObject(null)
                glob.SetValue("aardvark", target, CefV8PropertyAttribute.DontDelete) 
                    |> check "could not set global aardvark-value"

                let aardvark = AardvarkIO(browser, ctx)
                aardvarks.[browser.Identifier] <- aardvark
                
                for name in aardvark.FunctionNames do
                    use f = CefV8Value.CreateFunction(name, aardvark)
                    target.SetValue(name, f, CefV8PropertyAttribute.DontDelete) 
                        |> check "could not attach function to aardvark-value"
            )

        override x.OnProcessMessageReceived(browser, source, msg) =
            match IPC.tryReadProcessMessage<Response> msg with
                | Some response ->
                    match aardvarks.TryGetValue browser.Identifier with
                        | (true, aardvark) ->
                            aardvark.got(response)
                        | _ ->
                            true
                | _ ->
                    base.OnProcessMessageReceived(browser, source, msg)

    open System.IO
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
                                            let dialog = 
                                                new OpenFileDialog(
                                                    Title = config.title,
                                                    Multiselect = config.allowMultiple,
                                                    InitialDirectory = getInitialPath config.startPath
                                                )


                                            if config.filters.Length > 0 then
                                                dialog.Filter <- String.concat "|" config.filters
                                                dialog.FilterIndex <- config.activeFilter

                                            let res = dialog.ShowDialog()


                                            match res with
                                                | DialogResult.OK -> 
                                                    let files = dialog.FileNames
                                                    let path = files |> Seq.truncate 1 |> Seq.map Path.GetDirectoryName |> Seq.tryHead
                                                    match path with
                                                        | Some p -> setPath p
                                                        | None -> ()

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

    type MyCefApp() =
        inherit CefApp()

        let handler = RenderProcessHandler()

        override x.GetRenderProcessHandler() =
            handler :> CefRenderProcessHandler


    let mutable private initialized = false
    let l = obj()
    let init argv =
        lock l (fun _ -> 
            if not initialized then
                initialized <- true

                CefRuntime.Load()

                let settings = CefSettings()
                settings.MultiThreadedMessageLoop <- CefRuntime.Platform = CefRuntimePlatform.Windows;
                settings.SingleProcess <- false;
                settings.LogSeverity <- CefLogSeverity.Default;
                settings.LogFile <- "cef.log";
                settings.ResourcesDirPath <- System.IO.Path.GetDirectoryName(Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase).LocalPath);
                settings.RemoteDebuggingPort <- 1337;
                settings.NoSandbox <- true;
                let args = 
                    if CefRuntime.Platform = CefRuntimePlatform.Windows then argv
                    else Array.append [|"-"|] argv

                let mainArgs = CefMainArgs(argv)
                let app = MyCefApp()
                let code = CefRuntime.ExecuteProcess(mainArgs,app,IntPtr.Zero)
                if code <> -1 then System.Environment.Exit code

                CefRuntime.Initialize(mainArgs,settings,app,IntPtr.Zero)

                Application.ApplicationExit.Add(fun _ -> 
                    CefRuntime.Shutdown()
                )
                AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> 
                    CefRuntime.Shutdown()
                )
        )
 
type AardvarkCefBrowser() =
    inherit CefWebBrowser()

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