
open System
open System.Windows.Forms

open Scratch
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Rendering.GL

open Aardvark.Cef

module Shader =
    open FShade

    let browserSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let fullScreen (v : Effects.Vertex) =
        fragment {
            let coord = V2d(0.5 + 0.5 * v.pos.X, 0.5 - 0.5 * v.pos.Y)
            let pixel = V2d uniform.ViewportSize * coord |> V2i
            let textureSize = browserSampler.Size
            
            if pixel.X < textureSize.X && pixel.Y < textureSize.Y then
                return browserSampler.[pixel]
            else
                return V4d(0.0,0.0,0.0,0.0)
        }

    let overlay (v : Effects.Vertex) =
        fragment {
            let coord = V2d(0.5 + 0.5 * v.pos.X, 0.5 + 0.5 * v.pos.Y)
            let textureSize = browserSampler.Size
            let offset : V2i = uniform?TextureOffset

            //let offset = V2i(offset.X, uniform.ViewportSize.Y - offset.Y - 1)

            let pixel = (V2d uniform.ViewportSize * coord |> V2i) - offset  

            if pixel.X < textureSize.X && pixel.Y < textureSize.Y && pixel.X >= 0 && pixel.Y >= 0 then
                let color = browserSampler.[pixel]
                return color
            else
                return V4d(0.0,0.0,0.0,0.0)
        }

    let overlay2 (v : Effects.Vertex) =
        fragment {
            return browserSampler.SampleLevel(v.tc, 0.0) + V4d(1.5,0.5,0.5,1.0)
        }


module ElmishService =
    open Fablish
    open Aardvark.Elmish
    open Aardvark.Service

    let run (runtime : IRuntime) (threeD : string -> IRenderControl -> Elmish.Running<'model, 'msg>) =
        Aardvark.Service.Server.start runtime 8989 [] (fun id ctrl ->
            let task = runtime.CompileRender(ctrl.FramebufferSignature, BackendConfiguration.Default, (threeD id ctrl).sg)
            Some task
        )


[<AutoOpen>]
module MixedAppImpl = 
    open Fablish
    open Fable.Helpers.Virtualdom
    open Fable.Helpers.Virtualdom.Html
    open Aardvark.ImmutableSceneGraph

    module MixedApp =


        let serve2' (runtime : IRuntime) (u : Aardvark.Elmish.Unpersist<'m3, 'mm3>) (app : App<'mui, 'aui, DomNode<'aui>>) (threeD : Aardvark.Elmish.App<'m3, 'mm3, 'a3, Aardvark.ImmutableSceneGraph.ISg<'a3>>) =
            
            let initial = app.initial, threeD.initial

            let update (e : Env<Choice<'aui, 'a3>>) ((mui,m3)) (msg : Choice<'aui, 'a3>) =
                match msg with
                    | Choice1Of2 aui -> 
                        (app.update (Env.map Choice1Of2 e) mui aui, m3)
                    | Choice2Of2 a3 ->
                        (mui, threeD.update (Env.map Choice2Of2 e) m3 a3)

            let composed = ComposedApp.ofUpdate initial update
                     
            let fablishResult = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" app (fun m (_,t) -> (m,t)) fst Choice1Of2

            Async.Start <|
                async {
                    do! Async.SwitchToNewThread()
                    ElmishService.run runtime (fun name win ->
                        let three3dApp = threeD
                                
                        let cameraView = 
                            CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                                //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                                |> Mod.constant 

                        let frustum = 
                            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))


                        let camera = Mod.map2 Camera.create cameraView frustum

                        let add3d (ctrl : IRenderControl) (camera : IMod<Camera>) (app : Aardvark.Elmish.App<'m3,'mm3,'a3,_>) =
                            let doUpdate (m : 'm3) (msg : 'a3) =
                                lock composed (fun _ -> 
                                    let bigModel = m
                                    let bigMsg = Choice2Of2 msg
                                    let newBigModel = composed.Update bigMsg
                                    for a in composed.InnerApps do a newBigModel
                                    let _,m = newBigModel
                                    m
                                )

                            let instance = Aardvark.Elmish.Elmish.createAppAdaptive ctrl camera u (Some doUpdate) app
                            composed.Register(fun (_,m) -> lock composed (fun _ -> instance.emitModel m)) 
                            instance

                        let three3dInstance = add3d win camera three3dApp
                        three3dInstance
                    )
                }

            fablishResult   
        
        let inline serve2 (runtime : IRuntime) (app : App<'mui, 'aui, DomNode<'aui>>) (threeD : Aardvark.Elmish.App<'m3, 'mm3, 'a3, Aardvark.ImmutableSceneGraph.ISg<'a3>>) =
            let toMod m c = (^m3 : (member ToMod : ReuseCache -> 'mm3) (m,c))
            let apply i m c = (^mm3 : (member Apply : ^m3 * ReuseCache -> unit) (m,i,c))
            serve2'
                runtime
                { Aardvark.Elmish.Unpersist.unpersist = toMod; Aardvark.Elmish.Unpersist.apply = apply }
                app
                threeD



[<EntryPoint;STAThread>]
let main argv = 
  //  InteractionTest.run()
  //  InteractionTest.fablishTest()
   // System.Environment.Exit 0

    Chromium.init()

    Aardvark.SceneGraph.IO.Loader.Assimp.initialize()

    //let splashScreen = SplashScreen.spawn()
 
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    //use win = app.CreateSimpleRenderWindow()
//    win.Text <- "Aardvark rocks media \\o/"
//    win.Load.Add(fun _ -> win.Size <- V2i(0,0))
//    win.FormBorderStyle <- FormBorderStyle.None
    //let mutable started = false

    let desiredSize = V2i(1280,800)

    let browser = new Xilium.CefGlue.WindowsForms.CefWebBrowser()
    let form = new System.Windows.Forms.Form(Width = 1024, Height = 768)
    browser.Dock <- DockStyle.Fill
    form.Controls.Add browser

    let er, fi = DrawingApp.createApp (Some 0)
    browser.StartUrl <- fi.localUrl

    Async.Start <|
        async {
            do! Async.SwitchToNewThread()
            ElmishService.run app.Runtime (fun name win ->
                let cameraView = 
                    CameraView.lookAt (V3d(3.0, 3.0, 3.0)) V3d.Zero V3d.OOI
                        |> Mod.constant
                        //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time

                let frustum = 
                    win.Sizes
                        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

                let camera = Mod.map2 Camera.create cameraView frustum

                let three3dInstance = er win camera
                three3dInstance
            )
        }
//
//
//    let fablishResult = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" TestApp.app (fun m app -> { app with ui = m}) (fun app -> app.ui) Explicit.AppMsg.UiMsg
//    browser.StartUrl <- fablishResult.localUrl


    Application.Run form


    //client.LoadUrlAsync fablishResult.localUrl |> ignore



//
//
//
//
//    
//
//
//
//    let mutable lastSize = 256 * V2i.II
//    let renderControlViewport =
//        adaptive {
//            let! (size,version) = client.Size, client.Version
//            let vp = client.GetViewport "renderControl"
//            match vp with
//                | Some vp ->
//                    lastSize <- vp.Size
//
//                    if not started then
//                        started <- true
//                        let fixup () = win.BeginInvoke(Action(fun _ -> win.FormBorderStyle <- FormBorderStyle.Sizable; (win :> System.Windows.Forms.Form).Size <- Drawing.Size(desiredSize.X, desiredSize.Y); splashScreen.Close())) |> ignore
//                        if win.IsHandleCreated then
//                            fixup()
//                        else win.HandleCreated.Add(fun _ -> fixup())
//
//                    let vp2 = Box2i(V2i(vp.Min.X, size.Y - vp.Max.Y), V2i(vp.Max.X, size.Y - vp.Min.Y))
//                    return vp2
//
//                | None ->
//                    return Box2i(V2i.OO,lastSize)
//        }
//
//    let renderRect = renderControlViewport |> Mod.map (fun s -> Box2i.FromMinAndSize(V2i(s.Min.X,win.Size.Y - s.Max.Y),s.Size))
//    let cameraView = 
//        CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
//            //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time
//            |> Mod.constant 
//
//    let frustum = 
//        renderControlViewport
//            |> Mod.map (fun b -> let s = b.Size in Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
//
//
//    let camera = Mod.map2 Camera.create cameraView frustum
//
//    let sg, shutdown =
//        if false then
//            let composed = ComposedApp.ofUpdate  { Explicit.ui = TestApp.initial; Explicit.scene = TranslateController.initial } Explicit.update
//            let three3dApp = TranslateController.app (renderControlViewport |> Mod.map (fun (b : Box2i) -> b.Size))
//
//            let three3dInstance = ComposedApp.add3d composed win.Keyboard win.Mouse renderRect camera three3dApp (fun m app -> { app with scene = m }) (fun app -> app.scene) Explicit.AppMsg.SceneMsg
//            let fablishResult = ComposedApp.addUi composed Net.IPAddress.Loopback "8083" TestApp.app (fun m app -> { app with ui = m}) (fun app -> app.ui) Explicit.AppMsg.UiMsg
//
//            let res = client.LoadUrlAsync fablishResult.localUrl
//            three3dInstance.sg, fablishResult.shutdown
//        else 
//            //let three3dInstance, fablishResult = SingleMultiView.createApp win.Keyboard win.Mouse renderRect camera
//            //let three3dInstance, fablishResult = ModelingTool.createApp win win.Time win.Keyboard win.Mouse renderRect camera
//            let three3dInstance, fablishResult = ComposeTest.createApp (Some 0) win.Time win.Keyboard win.Mouse renderRect camera
//            let res = client.LoadUrlAsync fablishResult.localUrl
//            three3dInstance.sg, fablishResult.shutdown
//
//    let fullscreenBrowser =
//        Sg.fullScreenQuad
//            |> Sg.diffuseTexture client.Texture 
//            |> Sg.effect [
//                Shader.fullScreen |> toEffect
//               ]
//            |> Sg.blendMode (Mod.constant BlendMode.Blend)
//            |> Sg.pass (RenderPass.after "gui" RenderPassOrder.Arbitrary RenderPass.main)
//    
//
//    let scene =
//        Sg.box' C4b.White Box3d.Unit
//            |> Sg.effect [
//                DefaultSurfaces.trafo          |> toEffect           
//                DefaultSurfaces.constantColor C4f.Blue |> toEffect  
//                ]
//            |> Sg.viewTrafo (Mod.map CameraView.viewTrafo cameraView)
//            |> Sg.projTrafo (Mod.map Frustum.projTrafo frustum)
//
//
//
//    let sceneTask = 
//        RenderTask.ofList [
//            app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Gray)
//            app.Runtime.CompileRender(win.FramebufferSignature, sg)
//        ]
//    let sceneSize = renderControlViewport |> Mod.map (fun box -> box.Size )
//    let renderContent = RenderTask.renderToColor sceneSize sceneTask
//
//    let renderOverlay =
//        Sg.fullScreenQuad
//            |> Sg.diffuseTexture renderContent 
//            |> Sg.effect [
//                Shader.overlay |> toEffect
//               ]
//            |> Sg.uniform "TextureOffset" (renderControlViewport |> Mod.map (fun box -> box.Min ))
//
//
//    let composite = Sg.ofSeq [renderOverlay; fullscreenBrowser] |> Sg.depthTest (Mod.constant DepthTestMode.None)
//    
//    client.SetFocus true
//    client.Mouse.Use(win.Mouse) |> ignore
//    client.Keyboard.Use(win.Keyboard) |> ignore
//
//    let task =
//        RenderTask.ofList [
//            app.Runtime.CompileRender(win.FramebufferSignature, composite)
//        ]
//
//    
//    win.RenderTask <- task
//    
//    win.Run()
//
//    shutdown()
    Chromium.shutdown()
    0