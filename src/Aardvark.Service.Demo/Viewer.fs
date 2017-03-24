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

module Chromium =

    open System.Windows.Forms
    open System.Threading

    open Xilium.CefGlue.Wrapper
    open Xilium.CefGlue

    type MyCefApp() =
        inherit CefApp()


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

    type Message =
        | Accept 
        | Nope
        | OpenFile

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
    let update (model : ViewerModel) (msg : Message) =
        match msg with
            | OpenFile ->
                let res = openDialog()
                { model with file = res }

            | Accept ->
                match model.file with
                    | Some file ->
                        printfn "importing: %A" file
                        { model with file = None }
                    | None ->
                        model

            | Nope -> 
                { model with file = None }


    let view (model : MViewerModel) =
        let cam =
            Mod.constant (Camera.create (CameraView.lookAt (V3d(3.0, 4.0, 5.0)) V3d.Zero V3d.OOI) (Frustum.perspective 60.0 0.1 100.0 1.0))
        require semui [
            div' [clazz "pushable"] [

                div' [ clazz "ui sidebar inverted vertical menu wide" ] [
                    div' [ clazz "item"] [ 
                        b' [] [text' "File"]
                        div' [ clazz "menu" ] [
                            div' [clazz "item"] [
                                button' [clazz "ui button"; clientEvent "onclick" "$('.ui.modal').modal('show'); $('#n14').dropdown();"] [
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

//                    div' [clazz "aardvark"; attribute "style" "width:100%; height: 100%"] [
//                        img' [clazz "rendercontrol loading"]
//                    ]

                    renderControl'
                        cam
                        [
                            attribute "style" "width:100%; height: 100%"
                        ]
                        (
                            Sg.box' C4b.Green (Box3d(-V3d.III, V3d.III))
                                |> Sg.shader {
                                    do! DefaultSurfaces.trafo
                                    do! DefaultSurfaces.vertexColor
                                    do! DefaultSurfaces.simpleLighting
                                }
                                |> Sg.noEvents
                        )


                    onBoot "$('#__ID__').modal({ onApprove: function() { $('.sidebar').sidebar('hide'); } });" [
                        div' [clazz "ui modal"] [
                            i' [clazz "close icon"] []
                            div' [clazz "header"] [text' "Open File"]
                            div' [clazz "content"] [
                                button' [clazz "ui button"; onClick (fun _ -> OpenFile)] [text' "browse"]
                                br' []
                                text (
                                    model.file |> Mod.map (fun str -> 
                                        match str with
                                            | Some str -> System.Web.HtmlString("file: " + str.Replace("\\", "\\\\")).ToHtmlString()
                                            | None -> "please select a file"
                                    )
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
                                div' [clazz "ui button deny"; onClick (fun _ -> Nope)] [text' "Cancel"]
                                div' [clazz "ui button positive"; onClick (fun _ -> Accept)] [text' "Open"]
                            ]
                        ]
                    ]
                ]
            ]

        ]

    let app =
        {
            initial = { file = None }
            update = update
            view = view
        }



    let run argv = 
        ChromiumUtilities.unpackCef()
        Chromium.init argv

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
        use ctrl = new Xilium.CefGlue.WindowsForms.CefWebBrowser()
        ctrl.Dock <- DockStyle.Fill
        form.Controls.Add ctrl
        ctrl.StartUrl <- "http://localhost:4321/main/"

        Application.Run form