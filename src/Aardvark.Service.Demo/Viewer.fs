namespace Viewer

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text
open Xilium.CefGlue
open Xilium.CefGlue.Platform.Windows
open Xilium.CefGlue.WindowsForms
open System.Windows.Forms
open Aardvark.SceneGraph.IO
open System.Windows.Forms
open Viewer
open Demo

open System.IO

module Chromium =
    open System.Reflection
    open System.Windows.Forms
    open System.Threading

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

                                            ()
                                    | _ -> 
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




type FSEntryKind =
    | Unknown = 0
    | File = 1
    | Directory = 2
    | Root = 3
    | Disk = 4
    | DVD = 5
    | Share = 6
    | Removable = 7

type FSEntry =
    {
        kind            : FSEntryKind
        isDevice        : bool
        path            : string
        name            : string
        length          : int64
        lastWriteTime   : DateTime
        lastAccessTime  : DateTime
        creationTime    : DateTime
        hasChildren     : bool
        hasFolders      : bool
        isHidden        : bool
        isSystem        : bool
    }

type FSContent =
    {
        success : bool
        fullPath : string
        entries : list<FSEntry>
    }

type FileSystem private(rootPath : Option<string>) =
    let rootPath =
        match rootPath with
            | Some p -> 
                let p = Path.GetFullPath p 
                if p.EndsWith "\\" then p.Substring(0, p.Length - 1) |> Some
                else p |> Some
            | None -> 
                None

    let rec appendPath (p : string) (c : list<string>) =
        match c with
            | [] -> Some p
            | ".." :: rest -> appendPath (Path.GetDirectoryName(p)) rest
            | "." :: rest -> appendPath p rest
            | h :: rest -> appendPath (Path.Combine(p, h)) rest

    let localPath (path : string) =
        let path = 
            if path.StartsWith "/" then path.Substring(1)
            else path

        let parts = path.Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
            

        match rootPath, parts with
            | Some r, parts -> 
                match appendPath r parts with
                    | Some p ->
                        if p.StartsWith r then Some p
                        else None
                    | None ->
                        None
            | None, r :: parts ->
                match Environment.OSVersion with
                    | Windows -> appendPath (r + ":\\") parts
                    | _ -> appendPath "" (r :: parts)

            | None, [] ->
                Some "/"
                                
    let remotePath (fullPath : string) =

        let sep =
            match Environment.OSVersion with
                | Windows -> '\\'
                | _ -> '/'

        let relative = 
            match rootPath with
                | Some root -> 
                    if fullPath.StartsWith root then
                        Some(fullPath.Substring(root.Length).Split([|sep|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
                    else
                        None
                | None -> 
                    let comp = fullPath.Split([|sep|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
                    match Environment.OSVersion, comp with
                        | Windows, h :: rest ->
                            Some (h.Substring(0, h.Length - 1 ) :: rest)
                        | _ ->
                            Some comp

             
        relative |> Option.map (String.concat "/" >> sprintf "/%s")
                                    

    let createEntry (path : string) =
        match remotePath path with
            | Some remotePath ->
                let att = File.GetAttributes(path)
                if att.HasFlag FileAttributes.Directory then
                    let d = DirectoryInfo(path)

                    let hasChildren =
                        try d.EnumerateFileSystemInfos() |> Seq.isEmpty |> not
                        with _ -> false

                    let hasFolders =
                        if hasChildren then
                            try d.EnumerateDirectories() |> Seq.isEmpty |> not
                            with _ -> false
                        else
                            false

                    Some {
                        kind            = FSEntryKind.Directory
                        isDevice        = false
                        path            = remotePath
                        name            = d.Name
                        length          = 0L
                        lastWriteTime   = d.LastWriteTimeUtc
                        lastAccessTime  = d.LastAccessTimeUtc
                        creationTime    = d.CreationTimeUtc
                        hasChildren     = hasChildren
                        hasFolders      = hasFolders
                        isHidden        = att.HasFlag FileAttributes.Hidden
                        isSystem        = att.HasFlag FileAttributes.System
                    }

                else
                    let f = FileInfo(path)
                
                    Some {
                        kind            = FSEntryKind.File
                        isDevice        = false
                        path            = remotePath
                        name            = f.Name
                        length          = f.Length
                        lastWriteTime   = f.LastWriteTimeUtc
                        lastAccessTime  = f.LastAccessTimeUtc
                        creationTime    = f.CreationTimeUtc
                        hasChildren     = false
                        hasFolders      = false
                        isHidden        = att.HasFlag FileAttributes.Hidden
                        isSystem        = att.HasFlag FileAttributes.System
                    }
            | None -> 
                None
        
    let rootEntries =
        DriveInfo.GetDrives()
            |> Array.toList
            |> List.map (fun di ->
                let kind =
                    match di.DriveType with
                        | DriveType.Fixed -> FSEntryKind.Disk
                        | DriveType.Network -> FSEntryKind.Share
                        | DriveType.Removable -> FSEntryKind.Removable
                        | DriveType.CDRom -> FSEntryKind.DVD
                        | _ -> FSEntryKind.Unknown

                let name = di.Name.Substring(0, di.Name.Length - 2)

                let hasChildren, length =
                    if di.IsReady then
                        try 
                            let empty = DirectoryInfo(di.Name).EnumerateFiles() |> Seq.isEmpty
                            not empty, di.TotalSize
                        with _ -> 
                            false, 0L
                    else
                        false, 0L

                {
                    kind            = kind
                    isDevice        = true
                    path            = "/" + name
                    name            = name
                    length          = length
                    lastWriteTime   = DateTime.MinValue
                    lastAccessTime  = DateTime.MinValue
                    creationTime    = DateTime.MinValue
                    hasChildren     = hasChildren
                    hasFolders      = hasChildren
                    isHidden        = false
                    isSystem        = false
                }

            )


    member x.GetEntries(path : string) =
        match Environment.OSVersion, localPath path with
            | Windows, Some "/" ->
                {
                    success = true
                    fullPath = "/"
                    entries = rootEntries
                }

            | _,Some localPath ->
                if Directory.Exists localPath then
                    let entries = 
                        Directory.GetFileSystemEntries(localPath)
                            |> Array.toList
                            |> List.choose createEntry
                    {
                        success = true
                        fullPath = path
                        entries = entries
                    }
                else
                    {
                        success = false
                        fullPath = path
                        entries = []
                    }
            | _,None ->
                {
                    success = false
                    fullPath = path
                    entries = []
                }
          

    new(rootDir : string) =
        if not (Directory.Exists rootDir) then failwithf "[FS] cannot open directory: %A" rootDir
        FileSystem(Some rootDir)

    new() = FileSystem(None)
    

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FileSystem =
    open Suave
    open Suave.Filters
    open Suave.Operators
    open Suave.Successful
    
    let toWebPart (fs : FileSystem) : WebPart =
        fun (ctx : HttpContext) ->
            async {
                let entries = 
                    match ctx.request.queryParam "path" with
                        | Choice1Of2 p -> fs.GetEntries p
                        | _ -> fs.GetEntries "/"

                let data = Pickler.json.Pickle entries

                return! ctx |> (ok data >=> Writers.setMimeType "text/json")
            }

    

     
module Viewer =
    
    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]  

    module SemUi =
        
        let dropDown<'a, 'msg when 'a : enum<int> and 'a : equality> (selected : IMod<'a>) (change : 'a -> 'msg) =
            let names = Enum.GetNames(typeof<'a>)
            let values = Enum.GetValues(typeof<'a>) |> unbox<'a[]>
            let nv = Array.zip names values

            let attributes (name : string) (value : 'a) =
                AttributeMap.ofListCond [
                    always (attribute "value" name)
                    onlyWhen (Mod.map ((=) value) selected) (attribute "selected" "selected")
                ]

            onBoot "$('#__ID__').dropdown();" (
                select [clazz "ui dropdown"; onChange (fun str -> Enum.Parse(typeof<'a>, str) |> unbox<'a> |> change)] [
                    for (name, value) in nv do
                        let att = attributes name value
                        yield Incremental.option att (AList.ofList [text name])
                ]
            )

        let menu (c : string )(entries : list<string * list<DomNode<'msg>>>) =
            div [ clazz c ] (
                entries |> List.map (fun (name, children) ->
                    div [ clazz "item"] [ 
                        b [] [text name]
                        div [ clazz "menu" ] (
                            children |> List.map (fun c ->
                                div [clazz "item"] [c]
                            )
                        )
                    ]
                )
            )

    let sw = System.Diagnostics.Stopwatch()
    

    let update (model : ViewerModel) (msg : Message) =
        match msg with
            | TimeElapsed ->
                let dt = sw.Elapsed.TotalSeconds
                sw.Restart()
                { model with rotation = model.rotation + 0.5 * dt }

            | OpenFile files ->
                printfn "files: %A" files
                { model with files = files }

            | Import ->
                Log.startTimed "importing %A" model.files
                let scenes = model.files |> HSet.ofList |> HSet.map (Loader.Assimp.load)
                let bounds = scenes |> Seq.map (fun s -> s.bounds) |> Box3d
                let sgs = scenes |> HSet.map Sg.adapter
                Log.stop()
                { model with files = []; scenes = sgs; bounds = bounds }

            | Cancel ->
                { model with files = [] }

            | CameraMessage msg ->
                Log.line "cam: %A" msg
                { model with camera = CameraController.update model.camera msg }

            | SetFillMode mode ->
                { model with fillMode = mode }

            | SetCullMode mode ->
                { model with cullMode = mode }

    


    let view (model : MViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
//            |> Mod.map (fun view -> Camera.create view (Frustum.perspective 60.0 0.1 100.0 1.0))

        let normalizeTrafo (b : Box3d) =
            let size = b.Size
            let scale = 4.0 / size.NormMax

            let center = b.Center

            Trafo3d.Translation(-center) *
            Trafo3d.Scale(scale)


        require semui (
            div [clazz "pushable"] [
                SemUi.menu "ui sidebar inverted vertical menu wide" [

                    "File", 
                    [
                        button [clazz "ui button"; clientEvent "onclick" "$('.ui.modal').modal('show');"] [
                            text "Import"
                        ]
                    ]

                    "Rendering", 
                    [
                        table [clazz "ui celled striped table inverted"] [
                            tr [] [
                                td [clazz "right aligned"] [
                                    text "FillMode:"
                                ]
                                td [clazz "right aligned"] [
                                    SemUi.dropDown model.fillMode SetFillMode
                                ]
                            ]

                            tr [] [
                                td [clazz "right aligned"] [
                                    text "CullMode:"
                                ]
                                td [clazz "right aligned"] [
                                    SemUi.dropDown model.cullMode SetCullMode
                                ]
                            ]
                        ]
                    ]

                ]
                
                div [clazz "pusher"] [

                    div [
                        clazz "ui black big launch right attached fixed button menubutton"
                        js "onclick"        "$('.sidebar').sidebar('toggle');"
                    ] [
                        i [clazz "content icon"] [] 
                        span [clazz "text"] [text "Menu"]
                    ]
                    
                    
                    CameraController.controlledControl model.camera CameraMessage frustum
                        (AttributeMap.ofList [
                            attribute "style" "width:100%; height: 100%"
                            //onRendered (fun _ _ _ -> TimeElapsed)
                        ])
                        (
                            model.scenes
                                |> Sg.set
                                |> Sg.fillMode model.fillMode
                                |> Sg.cullMode model.cullMode
                                |> Sg.trafo (model.bounds |> Mod.map normalizeTrafo)
                                |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))
                                |> Sg.effect [
                                    toEffect DefaultSurfaces.trafo
                                    toEffect DefaultSurfaces.diffuseTexture
                                    toEffect DefaultSurfaces.simpleLighting
                                ]
                                |> Sg.trafo (model.rotation |> Mod.map Trafo3d.RotationZ)
                        )
                   
                    onBoot "$('#__ID__').modal({ onApprove: function() { $('.sidebar').sidebar('hide'); } });" (
                        div [clazz "ui modal"] [
                            i [clazz "close icon"] []
                            div [clazz "header"] [text "Open File"]
                            div [clazz "content"] [
                                button [
                                    clazz "ui button"
                                    onEvent "onchoose" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> OpenFile)
                                    clientEvent "onclick" ("aardvark.openFileDialog({ allowMultiple: true }, function(files) { if(files != undefined) aardvark.processEvent('__ID__', 'onchoose', files); });")
                                ] [ text "browse"]

                                br []
                                Incremental.div AttributeMap.empty (
                                    alist {
                                        let! files = model.files
                                        match files with
                                            | [] -> 
                                                yield text "please select a file"
                                            | files ->
                                                for f in files do
                                                    let str = System.Web.HtmlString("files: " + f.Replace("\\", "\\\\")).ToHtmlString()
                                                    yield text str
                                                    yield br []
                                    }
                                )

                            ]
                            div [clazz "actions"] [
                                div [clazz "ui button deny"; onClick (fun _ -> Cancel)] [text "Cancel"]
                                div [clazz "ui button positive"; onClick (fun _ -> Import)] [text "Import"]
                            ]
                        ]
                    )
                ]
            ]
        )

    let initial =
        {
            rotation = 0.0
            files = []
            scenes = HSet.empty
            bounds = Box3d.Unit
            fillMode = FillMode.Fill
            cullMode = CullMode.None
            camera =
                {
                    view = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
                    dragStart = V2i.Zero
                    look = false; zoom = false; pan = false
                    forward = false; backward = false; left = false; right = false
                    moveVec = V3i.Zero
                    lastTime = None
                    stash = None
                }
        }

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun model -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    open Suave.Filters
    open Suave.Operators
    open Suave.Successful
   

    let fstest() =
        let fs = FileSystem(@"C:\Program Files (x86)\Microsoft Visual Studio 14.0")
        Suave.WebPart.runServer 4321 [
            GET >=> path "/fs.json" >=> FileSystem.toWebPart fs
            GET >=> path "/" >=> (fun ctx -> ctx |> (OK (File.ReadAllText @"C:\Users\Schorsch\Desktop\fs.html")))
        ]
        Environment.Exit 0

    let run argv = 
        fstest()
        ChromiumUtilities.unpackCef()
        Chromium.init argv

        Loader.Assimp.initialize()

        Ag.initialize()
        Aardvark.Init()

        use gl = new OpenGlApplication()
        let runtime = gl.Runtime
    
        let mapp = App.start app
        let fs = FileSystem()

        let part = MutableApp.toWebPart runtime mapp
        Suave.WebPart.startServer 4321 [
            part
            GET >=> path "/fs.json" >=> FileSystem.toWebPart fs
        ]

        use form = new Form(Width = 1024, Height = 768)
        use ctrl = new AardvarkCefBrowser()
        ctrl.Dock <- DockStyle.Fill
        form.Controls.Add ctrl
        ctrl.StartUrl <- "http://localhost:4321/"

        //ctrl.ShowDevTools()

        Application.Run form