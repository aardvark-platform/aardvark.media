
open Aardvark.Service
open System

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
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
    //Aardvark.Cef.Internal.Cef.init' false

    let useVulkan = false

    Ag.initialize()
    Aardvark.Init()

    let app, runtime = 
        if useVulkan then
             let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(false) 
             app :> IDisposable, app.Runtime :> IRuntime
         else 
             let app = new OpenGlApplication()
             app :> IDisposable, app.Runtime :> IRuntime
    use app = app
    
    use form = new Form(Width = 1024, Height = 768)

    //let app = Viewer.KitchenSinkApp.app
    //let app = Aardvark.UI.Numeric.app
    //let app = TreeViewApp.app
    //let app = AnnotationProperties.app
    //let app = SimpleTestApp.app
    //let app = SimpleCompositionViewer.app
    //let app = OrbitCameraDemo.app
    //let app = ColorPicker.app
    //let app = Vector3d.app
   // let app = NavigationModeDemo.app
    //let app = Simple2DDrawingApp.app
    //let app = SimpleScaleApp.app

    //let app = DragNDrop.TranslateController.app
    //let app = DragNDrop.RotationController.app
    //let app = SimpleDrawingApp.app
    //let app = PlaceTransformObjects.App.app
    //let app = D3Axis.app
    //let app = D3Test.app
    //let app = BookmarkApp.app
    //let app = MeasurementsImporterApp.app form
    //let app = RenderModelApp.app 
    //let app = AnnotationApp.app
    //let app = DragNDrop.App.app
    let app = FalseColorLegendApp.app

    //let app = AnimationDemo.AnimationDemoApp.app

    //let app = OrthoCamera.OrthoCameraDemo.app

    //Config.shouldTimeUIUpdate <- true
    //Config.shouldTimeJsCodeGeneration <- true
    //Config.shouldTimeUnpersistCalls <- true


    //let app = PerformanceApp.app
    //let app = BoxSelectionDemo.app
    //let app = QuickTestApp.app

//    WebPart.startServer 4321 [ 
//        prefix "/twoD" >=> MutableApp.toWebPart runtime (QuickTestApp.app |> App.start)
//        prefix "/threeD" >=> MutableApp.toWebPart runtime (BoxSelectionDemo.app |> App.start)
//        MutableApp.toWebPart runtime (LayoutingApp.app "http://localhost:4321" |> App.start)
//        Suave.Files.browseHome
//    ]  


    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' runtime false (app |> App.start)
        Suave.Files.browseHome
    ] 

    //Console.ReadLine() |> ignore
    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"
    //ctrl.ShowDevTools()

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

