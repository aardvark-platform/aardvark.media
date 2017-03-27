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
open Viewer
open Demo

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

    type AardvarkIO(browser : CefBrowser, ctx : CefV8Context) as this =
        inherit CefV8Handler()
        let functions : Dictionary<string, CefV8Value[] -> CefResult> = 
            typeof<AardvarkIO>.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Seq.filter (fun mi -> mi.ReturnType = typeof<CefResult> && mi.GetParameters().Length = 1)
                |> Seq.map (fun mi -> mi.Name, FunctionReflection.buildFunction this mi)
                |> Dictionary.ofSeq

                
        let runner = ctx.GetTaskRunner()

        member x.FunctionNames = 
            printfn "NAMES: %A" (Seq.toList functions.Keys)
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

    
        member x.openFileDialog(args : CefV8Value[]) =
            
            let config =
                match args with
                    | [| FunctionValue f |] -> Some (OpenDialogConfig.empty, f)
                    | [| v; FunctionValue f |] -> Some(OpenDialogConfig.parse v, f)
                    | _ -> None

            match config with
                | Some (config, f) -> 
                    //System.Diagnostics.Debugger.Launch() |> ignore

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
                                            InitialDirectory = config.startPath
                                        )

                                    if config.filters.Length > 0 then
                                        dialog.Filter <- String.concat "|" config.filters
                                        dialog.FilterIndex <- config.activeFilter

                                    printfn "%A" dialog.Multiselect

                                    let res = dialog.ShowDialog()


                                    match res with
                                        | DialogResult.OK -> 
                                            let files = dialog.FileNames
                                            runner.PostTask {
                                                new CefTask() with
                                                    override x.Execute() =
                                                        ctx.Use (fun () ->
                                                            use arr = CefV8Value.CreateArray(files.Length)
                                                            for i in 0 .. files.Length - 1 do
                                                                use file = CefV8Value.CreateString files.[i]
                                                                arr.SetValue(i, file) |> ignore

                                                            f [| arr |] |> ignore
                                                        )
                                            } |> ignore
                                        | _ -> 
                                            runner.PostTask {
                                                new CefTask() with
                                                    override x.Execute() =
                                                        ctx.Use (fun () ->
                                                            f [| |] |> ignore
                                                        )
                                            } |> ignore

                    let thread = new Thread(ThreadStart(showDialog), IsBackground = true)
                    thread.SetApartmentState(ApartmentState.STA)
                    thread.Start()
                    NoRet
                | _ ->
                    Error "no argument for openFileDialog"




    let inline check str v = if not v then failwithf "[CEF] %s" str
   

    type RenderProcessHandler() =
        inherit CefRenderProcessHandler()

        override x.OnContextCreated(browser : CefBrowser, frame : CefFrame, ctx : CefV8Context) =
            base.OnContextCreated(browser, frame, ctx)

  
            ctx.Use (fun () ->
                use glob = ctx.GetGlobal()
                use target = CefV8Value.CreateObject(null)
                glob.SetValue("aardvarkio", target, CefV8PropertyAttribute.DontDelete) 
                    |> check "could not set global aardvark-value"

                let aardvark = AardvarkIO(browser, ctx)
                for name in aardvark.FunctionNames do
                    use f = CefV8Value.CreateFunction(name, aardvark)
                    target.SetValue(name, f, CefV8PropertyAttribute.DontDelete) 
                        |> check "could not attach function to aardvark-value"
            )


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
    
     
module Viewer =
    
    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]  


    let openDialog () =
        let mutable r = Unchecked.defaultof<_>
        let w = Application.OpenForms.[0]
        w.Invoke(Action(fun _ -> 
            let dialog = new OpenFileDialog()
            if dialog.ShowDialog() = DialogResult.OK then
                r <- Some dialog.FileName
            else r <- None
        )) |> ignore
        r

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
                { model with camera = CameraController.update model.camera msg }

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


        require semui [
            div' [clazz "pushable"] [

                div' [ clazz "ui sidebar inverted vertical menu wide" ] [
                    div' [ clazz "item"] [ 
                        b' [] [text' "File"]
                        div' [ clazz "menu" ] [
                            div' [clazz "item"] [
                                button' [clazz "ui button"; clientEvent "onclick" "$('.ui.modal').modal('show');"] [
                                    text' "Import"
                                ]
                            ]
                        ]
                    ]
                ]

                div' [clazz "pusher"] [

                    div' [
                        clazz "ui black big launch right attached fixed button menubutton"
                        js "onclick"        "$('.sidebar').sidebar('toggle');"
                    ] [
                        i' [clazz "content icon"] [] 
                        Ui("span", AMap.ofList [clazz "text"], Mod.constant "Menu")
                    ]

                    CameraController.controlledControl model.camera CameraMessage frustum
                        (AMap.ofList [
                            attribute "style" "width:100%; height: 100%"
                            //onRendered (fun _ _ _ -> TimeElapsed)
                        ])
                        (
                            model.scenes
                                |> Sg.set
                                |> Sg.trafo (model.bounds |> Mod.map normalizeTrafo)
                                |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))
                                |> Sg.effect [
                                    toEffect DefaultSurfaces.trafo
                                    toEffect DefaultSurfaces.diffuseTexture
                                    toEffect DefaultSurfaces.simpleLighting
                                ]
                                |> Sg.trafo (model.rotation |> Mod.map Trafo3d.RotationZ)
                        )


                    onBoot "$('#__ID__').modal({ onApprove: function() { $('.sidebar').sidebar('hide'); } });" [
                        div' [clazz "ui modal"] [
                            i' [clazz "close icon"] []
                            div' [clazz "header"] [text' "Open File"]
                            div' [clazz "content"] [
                                button' [
                                    clazz "ui button"
                                    onEvent "onchoose" ["event.files"] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> OpenFile)
                                    "onclick", ClientEvent(sprintf "aardvarkio.openFileDialog({ allowMultiple: true }, function(files) { if(files != undefined) aardvark.processEvent('%s', 'onchoose', files); });")
                                ] [ text' "browse"]

                                br' []
                                div AMap.empty (
                                    alist {
                                        let! files = model.files
                                        match files with
                                            | [] -> 
                                                yield text' "please select a file"
                                            | files ->
                                                for f in files do
                                                    let str = System.Web.HtmlString("files: " + f.Replace("\\", "\\\\")).ToHtmlString()
                                                    yield text' str
                                                    yield br' []
                                    }
                                )
//
//                                br' []
//
//                                text' "Up Direction: "
//
//
//                                select' [] [
//                                    option' [attribute "value" "x"] [text' "X"]
//                                    option' [attribute "value" "y"] [text' "Y"]
//                                    option' [attribute "value" "z"; attribute "selected" "selected"] [text' "Z"]
//                                ]
//
//                                br' []

                            ]
                            div' [clazz "actions"] [
                                div' [clazz "ui button deny"; onClick (fun _ -> Cancel)] [text' "Cancel"]
                                div' [clazz "ui button positive"; onClick (fun _ -> Import)] [text' "Import"]
                            ]
                        ]
                    ]
                ]
            ]

        ]

    let initial =
        {
            rotation = 0.0
            files = []
            scenes = HSet.empty
            bounds = Box3d.Unit
            camera =
                {
                    view = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
                    dragStart = V2i.Zero
                    look = false
                    zoom = false
                    pan = false
                    moveVec = V3i.Zero
                    lastTime = None
                }
        }

    let app =
        {
            initial = initial
            update = update
            view = view
        }


   
    let run argv = 
        ChromiumUtilities.unpackCef()
        Chromium.init argv

        Loader.Assimp.initialize()

        Ag.initialize()
        Aardvark.Init()

        use gl = new OpenGlApplication()
        let runtime = gl.Runtime
    

        Async.Start <|
            async {
                do! Async.SwitchToNewThread()
                App.start runtime 4321 app
            }


        use form = new Form(Width = 1024, Height = 768)
        use ctrl = new CefWebBrowser()
        ctrl.Dock <- DockStyle.Fill
        form.Controls.Add ctrl
        ctrl.StartUrl <- "http://localhost:4321/main/"

        Application.Run form