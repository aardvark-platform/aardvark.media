namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Scratch.DomainTypes

open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish
open Primitives

module TranslateController =

    open Aardvark.ImmutableSceneGraph
    open Aardvark.ImmutableSceneGraph.Scene
    open Primitives
    open Aardvark.Elmish

    open Scratch.DomainTypes

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

    open TranslateController

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initalModel = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; editTrafo = Trafo3d.Identity; _id = null }

    let initial =  { 
            scene = initalModel
            camera = Camera.create ( CameraView.lookAt (V3d.III*3.0) V3d.OOO V3d.OOI ) (Frustum.perspective 60.0 0.1 10.0 1.0)
            _id = null
        }


    let updateModel (m : TModel) (a : Action) =
        match a, m.activeTranslation with
            | NoHit, _             ->  { m with hovered = None; }
            | Hover (v,_), _       ->  { m with hovered = Some v}
            | Translate (axis,s), _ ->  { m with activeTranslation = Some (axis, Axis.moveAxis m.trafo axis, m.trafo.Backward.TransformPos s) }
            | EndTranslation, _    ->  { m with activeTranslation = None;  }
            | MoveRay r, Some (a,t,start) -> 
                let mutable ha = RayHit3d.MaxRange
                if r.HitsPlane(t,0.0,Double.MaxValue,&ha) then
                    let v = (ha.Point - start).XOO
                    { m with trafo = Trafo3d.Translation (ha.Point - start) }
                else m
            | MoveRay r, None -> m
            | ResetTrafo, _ -> { m with trafo = Trafo3d.Identity }

    let update e (m : Scene) (a : Action) =
        let scene = updateModel m.scene a
        { m with scene = scene }

    let viewModel (m : MTModel) =
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
                    [ arrow V3d.IOO |> render [on Mouse.move (hover X); on Mouse.down (translate_ X)] ]
                        |> colored (ifHit X C4b.White C4b.DarkRed)
                ]
                translate 0.0 1.0 0.0 [
                    [ arrow V3d.OIO |> render [on Mouse.move (hover Y); on Mouse.down (translate_ Y)] ]
                        |> colored (ifHit Y C4b.White C4b.DarkBlue)
                ]
                translate 0.0 0.0 1.0 [
                    [ arrow V3d.OOI |> render [on Mouse.move (hover Z); on Mouse.down (translate_ Z)] ]
                        |> colored (ifHit Z C4b.White C4b.DarkGreen)
                ]

                [ cylinder V3d.OOO V3d.IOO 1.0 0.05 |> render [ on Mouse.move (hover X); on Mouse.down (translate_ X) ] ] |> colored (ifHit X C4b.White C4b.DarkRed)
                [ cylinder V3d.OOO V3d.OIO 1.0 0.05 |> render [ on Mouse.move (hover Y); on Mouse.down (translate_ Y) ] ] |> colored (ifHit Y C4b.White C4b.DarkBlue)
                [ cylinder V3d.OOO V3d.OOI 1.0 0.05 |> render [ on Mouse.move (hover Z); on Mouse.down (translate_ Z) ] ] |> colored (ifHit Z C4b.White C4b.DarkGreen)
                
                translate 0.0 0.0 0.0 [
                    [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored (Mod.constant C4b.Gray)
                ]

                Everything |> render [whenever Mouse.move MoveRay]
        ]

    let viewScene (sizes : IMod<V2i>) s =   
        let cameraView = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))
        viewModel s.mscene 
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)
            |> effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]

    let ofPickMsgModel (model : TModel) (pick : GlobalPick) =
        match pick.mouseEvent with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> []
            | MouseEvent.Move when Option.isNone model.activeTranslation -> [NoHit]
            | MouseEvent.Move ->  []
            | MouseEvent.Up _   -> [EndTranslation]
            | MouseEvent.NoEvent -> []

    let ofPickMsg (model : Scene) noPick =
        ofPickMsgModel model.scene noPick

    let app (sizes : IMod<V2i>) = {
        initial = initial
        update = update
        view = viewScene sizes
        ofPickMsg = ofPickMsg
        subscriptions = Subscriptions.none
    }

module TranslateController2 =

    open Aardvark.ImmutableSceneGraph
    open Aardvark.ImmutableSceneGraph.Scene
    open Primitives
    open Aardvark.Elmish

    open Scratch.DomainTypes

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

    open TranslateController

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initalModel = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; editTrafo = Trafo3d.Identity; _id = null }

    let initial =  { 
            scene = initalModel
            camera = Camera.create ( CameraView.lookAt (V3d.III*3.0) V3d.OOO V3d.OOI ) (Frustum.perspective 60.0 0.1 10.0 1.0)
            _id = null
        }

    let updateModel (m : TModel) (a : Action) =
        match a, m.activeTranslation with
            | NoHit, _              ->  { m with hovered = None; }
            | Hover (v,_), _        ->  { m with hovered = Some v}
            | Translate (axis,s), _ ->  { m with activeTranslation = 
                                                    Some (axis,
                                                          Axis.moveAxis (m.trafo * m.editTrafo) axis,
                                                          m.editTrafo.Backward.TransformPos (s * Axis.dir axis)) } //
            | EndTranslation, _     ->  { m with activeTranslation = None; 
                                                 trafo = m.trafo * m.editTrafo; 
                                                 editTrafo = Trafo3d.Identity  }
            | MoveRay r, Some (axis,plane,start) ->
                let mutable ha = RayHit3d.MaxRange
                if r.HitsPlane(plane, 0.0, Double.MaxValue, &ha) then
                   let v = (ha.Point - start) * Axis.dir axis                   
                   { m with editTrafo = Trafo3d.Translation (v) }
                else m
            | MoveRay r, None -> m
            | ResetTrafo, _ -> { m with trafo = Trafo3d.Identity }

    let update e (m : Scene) (a : Action) =
        let scene = updateModel m.scene a
        { m with scene = scene }

    let cRadius = 0.03
    let aRadius = cRadius * 2.0
    let aHeight = aRadius * 3.0

    let viewCylinder (a : Axis) (c : C4b) ifHit =
         [ cylinder V3d.OOO (Axis.dir a) 1.0 cRadius 
            |> render [ on Mouse.move (hover a); on Mouse.down (translate_ a) ] 
         ] |> colored (ifHit a C4b.White c)

    let viewArrow (a : Axis) (c : C4b) ifHit =
        let arrow dir = Cone(V3d.OOO, dir, aHeight, aRadius)
        [ arrow (Axis.dir a) 
            |> render [on Mouse.move (hover a); on Mouse.down (translate_ a)]
        ]|> colored (ifHit a C4b.White c)

    let viewModel (m : MTModel) =
        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            adaptive {
                let! hovered = m.mhovered
                match hovered with
                    | Some v when v = a -> return selection
                    | _ -> return defaultColor
            }
        transform (m.mtrafo)[                   
            transform (m.meditTrafo) [
                
            //arrowheads
                    translate 1.0 0.0 0.0 [viewArrow X C4b.DarkRed ifHit]
                    translate 0.0 1.0 0.0 [viewArrow Y C4b.DarkGreen ifHit]
                    translate 0.0 0.0 1.0 [viewArrow Z C4b.DarkBlue ifHit]

            //cylinders                
                    viewCylinder X C4b.DarkRed ifHit
                    viewCylinder Y C4b.DarkGreen ifHit
                    viewCylinder Z C4b.DarkBlue ifHit

            //center sphere
                    translate 0.0 0.0 0.0 [
                        [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored (Mod.constant C4b.Gray)
                    ]

                    Everything |> render [whenever Mouse.move MoveRay]
            ]
        ]

    let viewScene (sizes : IMod<V2i>) s =   
        let cameraView = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))

        [[Quad3d [| V3d(-2,-2,0); V3d(2,-2,0); V3d(2,2,0); V3d(-2,2,0) |] |> Quad |> render Pick.ignore ]|> colored (Mod.constant C4b.Gray);
        viewModel s.mscene ]
            |> Scene.group
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)
            |> effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]

    let ofPickMsgModel (model : TModel) (pick : GlobalPick) =
        match pick.mouseEvent with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> []
            | MouseEvent.Move when Option.isNone model.activeTranslation -> [NoHit]
            | MouseEvent.Move ->  []
            | MouseEvent.Up _   -> [EndTranslation]
            | MouseEvent.NoEvent -> []

    let ofPickMsg (model : Scene) noPick =
        ofPickMsgModel model.scene noPick

    let app (sizes : IMod<V2i>) = {
        initial = initial
        update = update
        view = viewScene sizes
        ofPickMsg = ofPickMsg
        subscriptions = Subscriptions.none
    }