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
        require semui [
            button' [clazz "ui button"; clientEvent "onclick" "$('.ui.modal').modal('show');"] [
                text' "Import"
            ]

            onBoot "$('#__ID__').modal();" [
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

                        br' []

                        text' "Up Direction: "

                        onBoot "setTimeout(0);$(__ID__).dropdown();" [
                            div' [clazz "ui dropdown"] [
                                input' [attribute "type" "hidden"; attribute "name" "updir"]
                                i' [clazz "dropdown icon"] []
                                div' [clazz "default text"] [ text' "Up Direction"]
                                div' [clazz "menu"] [
                                    div' [clazz "item"; attribute "data-value" "x"] [text' "X"]
                                    div' [clazz "item"; attribute "data-value" "y"] [text' "Y"]
                                    div' [clazz "item"; attribute "data-value" "z"] [text' "Z"]
                                ]
                            ]
                        ]
//                        select' [clazz "ui dropdown mini"; ] [
//                            option' [] [text' "X"]
//                            option' [] [text' "Y"]
//                            option' [attribute "selected" "selected"] [text' "Z"]
//                        ] 

                        br' []

                    ]
                    div' [clazz "actions"] [
                        div' [clazz "ui button deny"; onClick (fun _ -> Nope)] [text' "Cancel"]
                        div' [clazz "ui button positive"; onClick (fun _ -> Accept)] [text' "Open"]
                    ]
                ]
            ]
            //div' [clazz "ui modal"] []

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
                App.start runtime 1234 app
            }


        use form = new Form()
        use ctrl = new Xilium.CefGlue.WindowsForms.CefWebBrowser()
        ctrl.Dock <- DockStyle.Fill
        form.Controls.Add ctrl
        ctrl.StartUrl <- "http://localhost:1234/main/"

        Application.Run form