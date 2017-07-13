
open Aardvark.Service
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
open Demo.TestApp
open Demo.TestApp.Mutable

open Aardvark.SceneGraph
open Suave
open Suave.Operators
open Suave.Filters
open Suave.WebPart

open System.Windows.Forms
open UI.Composed
open PRo3DModels.Mutable



// elm intro
// Composition. how to
// how to make things composable (reusable components, higher order functions, guidlines model messages)
// testing (elm debugger)
// einbinden vong html javascript

(* TODO:
* plane pickshape
* Sg.shader  for ISg<'msg>
* multiple picks along pickray (e.g. pickthrough for example in when selecting objs etc)
* click has wrong semantics
* globalpicks should have firsthit property
*)


let kitchenSink argv =
    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv

    Ag.initialize()
    Aardvark.Init()
    use app = new OpenGlApplication()
    let runtime = app.Runtime
    use form = new Form(Width = 1024, Height = 768)

    //let app = Viewer.KitchenSinkApp.app
    //let app = Aardvark.UI.Numeric.app
    //let app = TreeViewApp.app
    //let app = AnnotationProperties.app
    //let app = SimpleTestApp.app
    //let app = SimpleCompositionViewer.app
    let app = OrbitCameraDemo.app
    //let app = ColorPicker.app
    //let app = Vector3d.app
    //let app = NavigationModeDemo.app
    //let app = BoxSelectionDemo.app

    //let app = DragNDrop.TranslateController.app
    //let app = SimpleDrawingApp.app
    //let app = PlaceTransformObjects.App.app
    //let app = BookmarkApp.app
    //let app = MeasurementsImporterApp.app form
    //let app = RenderModelApp.app 
    //let app = AnnotationApp.app
    //let app = PerformanceApp.app
    
    let instance = 
        app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart runtime instance
        Suave.Files.browseHome
    ]  

    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"
    ctrl.ShowDevTools()

    Application.Run form
    System.Environment.Exit 0
    

let modelviewer args =
    Viewer.Viewer.run args
    System.Environment.Exit 0

[<EntryPoint; STAThread>]
let main args =

    kitchenSink args
    //modelviewer args

    0

