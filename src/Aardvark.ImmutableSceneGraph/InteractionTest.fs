namespace Scratch 

open System

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

open Aardvark.Elmish


module InteractionTest =

    let run () =
        Aardvark.Base.Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()

        let cameraView = 
            CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                |> Mod.constant
                //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let frustum = 
            win.Sizes
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

        let camera = Mod.map2 Camera.create cameraView frustum

        let bounds = win.Sizes |> Mod.map (fun s -> Box2i.FromMinAndSize(V2i.OO,s))

        let theApp = PlaceTransformObjects.app
        let theApp = SimpleDrawingApp.app
        //let theApp = CameraTest.app win.Time

        let adaptiveResult = Elmish.createAppAdaptiveD win.Keyboard win.Mouse bounds camera None theApp

        let sg = 
            //Elmish3D.createApp win camera TranslateController.app
            //Elmish3D.createApp win camera SimpleDrawingApp.app
            //Elmish3D.createApp win camera PlaceTransformObjects.app
            adaptiveResult.sg
            //view :> ISg

        let fullScene =
              sg 
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect       
                    DefaultSurfaces.vertexColor |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect 
                   ] 
                |> Sg.viewTrafo (Mod.map CameraView.viewTrafo cameraView)
                |> Sg.projTrafo (Mod.map Frustum.projTrafo frustum)

        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, fullScene)

        win.Run()