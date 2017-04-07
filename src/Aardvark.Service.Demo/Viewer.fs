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
                //Log.line "cam: %A" msg
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
                        button [clazz "ui button"; clientEvent "onclick" "$('.ui.modal[data-bla=hugo]').modal('show');"] [
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
                        div [clazz "ui modal"; attribute "data-bla" "hugo"] [
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
        use stream = typeof<FileSystem>.Assembly.GetManifestResourceStream("fs.html")
        let reader = new StreamReader(stream)
        let str = reader.ReadToEnd()

        let html() = 
            if Environment.MachineName.ToLower() = "monster64" then
                 File.ReadAllText @"E:\Development\aardvark-media\src\Aardvark.Service.Demo\fs.html"
            else
                str



        let fs = FileSystem()
        Suave.WebPart.runServer 4321 [
            GET >=> path "/fs.json" >=> FileSystem.toWebPart fs
            GET >=> path "/" >=> (fun ctx -> ctx |> OK (html()))
        ]
        Environment.Exit 0

    let run argv = 
        //fstest()
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