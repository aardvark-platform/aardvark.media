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

module DrawingApp =

    open Aardvark.ImmutableSceneGraph
    open Aardvark.Elmish
    open Primitives

    open SimpleDrawingApp

    type Action =
        | ClosePolygon
        | AddPoint   of V3d
        | MoveCursor of V3d

    let update (picking : Option<int>) e (m : Model) (cmd : Action) =
        match cmd, picking with
            | ClosePolygon, _ -> 
                match m.working with
                    | None -> m
                    | Some p -> 
                        { m with 
                            working = None 
                            finished = PSet.add p.finishedPoints m.finished
                        }
            | AddPoint p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = [ p ]; cursor = None;  }}
                    | Some v -> 
                        { m with working = Some { v with finishedPoints = p :: v.finishedPoints }}
            | MoveCursor p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}
            | _,_ -> m

    let sphereRadius = 0.025
    let cylinderRadius = 0.0125

    let viewPolygon (p : list<V3d>) =
        let lines =  Polygon3d(p |> List.toSeq).EdgeLines 
        [ for edge in lines |> Seq.take (Seq.length lines - 1)  do
            let v = edge.P1 - edge.P0
            yield Primitives.cylinder edge.P0 v.Normalized v.Length cylinderRadius |> Scene.render Pick.ignore 
            yield Sphere3d(edge.P0, sphereRadius) |> Sphere |> Scene.render Pick.ignore
        ] |> Scene.group

    let viewDrawingPolygons (m : MModel) =
        aset {
            for p in m.mfinished :> aset<_> do yield viewPolygon p

            let! working = m.mworking
            match working with
                | Some v when v.cursor.IsSome -> 
                    yield viewPolygon (v.cursor.Value :: v.finishedPoints)
                    yield 
                        [ Sphere3d(V3d.OOO, sphereRadius) |> Sphere |>  Scene.render Pick.ignore ] 
                            |> Scene.colored (Mod.constant C4b.Red)
                            |> Scene.transform' (Mod.constant <| Trafo3d.Translation(v.cursor.Value))
                | _ -> ()
        }
        
    let viewPlane = [ Quad (Quad3d [| V3d(-2,-2,0); V3d(2,-2,0); V3d(2,2,0); V3d(-2,2,0) |]) 
                            |>  Scene.render [ 
                                 on Mouse.move MoveCursor
                                 on (Mouse.down' MouseButtons.Left)  AddPoint 
                               //  on (Mouse.down' MouseButtons.Right) (constF ClosePolygon)
                               ] 
                      ] |>  Scene.colored (Mod.constant C4b.Gray)

    let view (m : MModel) = 
        let t = viewDrawingPolygons m
        Scene.agroup  t

    let viewScene (sizes : IMod<V2i>) (m : MModel) =
        let cameraView = CameraView.lookAt (V3d.IOO * 5.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))
        [view m
         Quad (Quad3d [| V3d(0,-2,-2); V3d(0,-2,2); V3d(0,2,2); V3d(0,2,-2) |]) 
            |> Scene.render [
                on  Mouse.move MoveCursor
                on (Mouse.down' MouseButtons.Left) AddPoint            
            ]
        ]
            |> Scene.group
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)
            |> Scene.effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]


    let subscriptions (m : Model) =
        Many [Input.key Down Keys.Enter (fun _ _-> ClosePolygon)]

    let initial = { finished = PSet.empty; working = None; _id = null }

    let app s =
        {
            initial = initial
            update = update (Some 0)
            view = viewScene s
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions
        }


