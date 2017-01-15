namespace InteractionTest 

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

module TranslateController =

    open AnotherSceneGraph
    open Elmish3DADaptive

    open Scratch.DomainTypes
    open TranslateController
    open Primitives

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Axis =
        let dir = function | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis
        let moveAxis (trafo : Trafo3d) = function
            | X -> Plane3d(trafo.Forward.TransformDir V3d.OOI, trafo.Forward.TransformPos V3d.OOO)
            | Y -> Plane3d(trafo.Forward.TransformDir V3d.OOI, trafo.Forward.TransformPos V3d.OOO)
            | Z -> Plane3d(trafo.Forward.TransformDir V3d.OIO, trafo.Forward.TransformPos V3d.OOO)

    type Action = 

        // hover overs
        | Hover           of Axis * V3d
        | NoHit       
        | MoveRay         of Ray3d

        // translations    
        | Translate       of Axis * V3d
        | EndTranslation 

        | ResetTrafo

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initial =  { 
            scene = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; _id = null }
            camera = Camera.create ( CameraView.lookAt (V3d.III*3.0) V3d.OOO V3d.OOI ) (Frustum.perspective 60.0 0.1 10.0 1.0)
            _id = null
        }


    let update  (m : Scene) (a : Action) =
        let scene =
            let m = m.scene
            match a, m.activeTranslation with
                | NoHit, _             ->  { m with hovered = None; }
                | Hover (v,_), _       ->  { m with hovered = Some v}
                | Translate (dir,s), _ -> { m with activeTranslation = Some (Axis.moveAxis m.trafo dir, m.trafo.Backward.TransformPos s) }
                | EndTranslation, _    -> { m with activeTranslation = None;  }
                | MoveRay r, Some (t,start) -> 
                    let mutable ha = RayHit3d.MaxRange
                    if r.HitsPlane(t,0.0,Double.MaxValue,&ha) then
                        let v = (ha.Point - start).XOO
                        { m with trafo = Trafo3d.Translation (ha.Point - start) }
                    else m
                | MoveRay r, None -> m
                | ResetTrafo, _ -> { m with trafo = Trafo3d.Identity }
        { m with scene = scene }

    let viewModel (m : MModel) =
        let arrow dir = Cone(V3d.OOO,dir,0.3,0.1)

        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            adaptive {
                let! hovered = m.mhovered
                match hovered with
                    | Some v when v = a -> return selection
                    | _ -> return defaultColor
            }
            
        transform m.mtrafo [
                translate 1.0 0.0 0.0 [
                    [ arrow V3d.IOO |> render [on Event.move (hover X); on Event.down (translate_ X)] ] 
                        |> colored (ifHit X C4b.White C4b.DarkRed)
                ]
                translate 0.0 1.0 0.0 [
                    [ arrow V3d.OIO |> render [on Event.move (hover Y); on Event.down (translate_ Y)] ] 
                        |> colored (ifHit Y C4b.White C4b.DarkBlue)
                ]
                translate 0.0 0.0 1.0 [
                    [ arrow V3d.OOI |> render [on Event.move (hover Z); on Event.down (translate_ Z)] ] 
                        |> colored (ifHit Z C4b.White C4b.DarkGreen)
                ]

                [ cylinder V3d.OOO V3d.IOO 1.0 0.05 |> render [ on Event.move (hover X); on Event.down (translate_ X) ] ] |> colored (ifHit X C4b.White C4b.DarkRed)
                [ cylinder V3d.OOO V3d.OIO 1.0 0.05 |> render [ on Event.move (hover Y); on Event.down (translate_ Y) ] ] |> colored (ifHit Y C4b.White C4b.DarkBlue)
                [ cylinder V3d.OOO V3d.OOI 1.0 0.05 |> render [ on Event.move (hover Z); on Event.down (translate_ Z) ] ] |> colored (ifHit Z C4b.White C4b.DarkGreen)
                
                translate 0.0 0.0 0.0 [
                    [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored (Mod.constant C4b.Gray)
                ]
        ]

    let viewScene cam s =   
        viewModel s.mscene 
            |> camera cam
            |> effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]

    let ofPickMsg (model : Scene) (NoPick(kind,ray)) =
        match kind with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> [NoHit]
            | MouseEvent.Move when Option.isNone model.scene.activeTranslation ->
                    [NoHit; MoveRay ray]
            | MouseEvent.Move ->  [MoveRay ray]
            | MouseEvent.Up _   -> [EndTranslation]

    let app (camera : IMod<Camera>) = {
        initial = initial
        update = update
        view = viewScene camera
        ofPickMsg = ofPickMsg
    }

module InteractionTest =

    let run () =

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

        let adaptiveResult = Elmish3DADaptive.createAppAdaptiveD win.Keyboard win.Mouse bounds camera SimpleDrawingApp.update SimpleDrawingApp.app

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