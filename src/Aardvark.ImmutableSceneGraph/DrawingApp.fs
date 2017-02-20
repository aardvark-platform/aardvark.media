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
        | Undo
        | Redo

    let update (picking : Option<int>) e (m : DrawingApp.Drawing) (cmd : Action) =
        match cmd, picking with
            | ClosePolygon, _ -> 
                match m.working with
                    | None -> m
                    | Some p -> 
                        { m with 
                            working = None 
                            finished = PSet.add p.finishedPoints m.finished
                            history = Some m
                            future = None
                        }
            | AddPoint p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = [ p ]; cursor = None;  }}
                    | Some v -> 
                        printfn "no of polys %A" m.finished.AsList.Length
                        { m with working = Some { v with finishedPoints = p :: v.finishedPoints }; history = Some m; future = None }
                        
            | MoveCursor p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}
            | Undo, _ -> match m.history with
                                | None -> m
                                | Some k -> { k with future = Some m }
            | Redo, _ -> match m.future with
                                | None -> m
                                | Some k -> k
            | _,_ -> m

    let sphereRadius = 0.015
    let cylinderRadius = 0.0125

    let viewPolygon (p : list<V3d>) =
        let lines =  Polygon3d(p |> List.toSeq).EdgeLines 
        [ for edge in lines |> Seq.take (Seq.length lines - 1)  do
            let v = edge.P1 - edge.P0
            yield Primitives.cylinder edge.P0 v.Normalized v.Length cylinderRadius |> Scene.render Pick.ignore 
            yield Sphere3d(edge.P0, sphereRadius) |> Sphere |> Scene.render Pick.ignore
        ] |> Scene.group

    let viewDrawingPolygons (m :  DrawingApp.MDrawing) =
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

    let view (m : DrawingApp.MDrawing) = 
        let t = viewDrawingPolygons m
        Scene.agroup  t

    let viewScene (sizes : IMod<V2i>) (m : DrawingApp.MDrawing) =
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


    let subscriptions (m : DrawingApp.Drawing) =
        Many [Input.key Down Keys.Enter (fun _ _-> ClosePolygon)
              Input.key Down Keys.Left  (fun _ _-> Undo)
              Input.key Down Keys.Right (fun _ _-> Redo)]

    let (initial : DrawingApp.Drawing) = { finished = PSet.empty; working = None; _id = null; history = None; future = None }

    let app s =
        {
            initial = initial
            update = update (Some 0)
            view = viewScene s
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions
        }


