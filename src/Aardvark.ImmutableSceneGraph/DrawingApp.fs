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
        | ChangeStyle of int
        | Undo
        | Redo
        | PickStart  
        | PickStop   

    let styles : List<DrawingApp.Style> = 
        [
           { color = new C4b(33,113,181) ; thickness = 0.03 }
           { color = new C4b(107,174,214); thickness = 0.02 }
           { color = new C4b(189,215,231); thickness = 0.01 }
           { color = new C4b(239,243,255); thickness = 0.005 }
        ]

    let update (picking : Option<int>) e (m : DrawingApp.Drawing) (cmd : Action) =
        let picking = m.picking
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
                    | None -> { m with working = Some { finishedPoints = [ p ]; cursor = None; }}
                    | Some v ->                         
                        { m with working = Some { v with finishedPoints = p :: v.finishedPoints }; history = Some m; future = None }
                        
            | MoveCursor p, Some _ ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}
            | ChangeStyle s, _ -> { m with style = styles.[s]}
            | Undo, _ -> match m.history with
                                | None -> m
                                | Some k -> { k with future = Some m }
            | Redo, _ -> match m.future with
                                | None -> m
                                | Some k -> k
            | PickStart, _   -> { m with picking = Some 0 }
            | PickStop, _    ->  
//                match m.working with
//                    | None -> { m with working = Some { finishedPoints = []; cursor = None }; picking = None}
//                    | Some v -> { m with working = Some { v with cursor = None }; picking = None}     
                { m with picking = None }             
            | _,_ -> m    

    let viewPolygon (p : list<V3d>) r =
        let lines =  Polygon3d(p |> List.toSeq).EdgeLines 
        [ for edge in lines |> Seq.take (Seq.length lines - 1)  do
            let v = edge.P1 - edge.P0
            yield Primitives.cylinder edge.P0 v.Normalized v.Length (r/2.0) |> Scene.render Pick.ignore 
            yield Sphere3d(edge.P0, r) |> Sphere |> Scene.render Pick.ignore
        ] |> Scene.group

    let viewDrawingPolygons (m :  DrawingApp.MDrawing) =
        aset {
            let! style = m.mstyle
            for p in m.mfinished :> aset<_> do                 
                yield [viewPolygon p style.thickness] |> Scene.colored (Mod.constant style.color)

            let! working = m.mworking
            match working with
                | Some v when v.cursor.IsSome -> 
                    yield 
                        [viewPolygon (v.cursor.Value :: v.finishedPoints) style.thickness] |> Scene.colored (Mod.constant style.color)
                    yield 
                        [ Sphere3d(V3d.OOO, style.thickness) |> Sphere |>  Scene.render Pick.ignore ] 
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

    let viewDrawing (m : DrawingApp.MDrawing) =         
        viewDrawingPolygons m 
            |> Scene.agroup 
            |> Scene.effect [
                    toEffect DefaultSurfaces.trafo;
                    toEffect DefaultSurfaces.vertexColor;]
                   // toEffect DefaultSurfaces.simpleLighting]

    let viewQuad (m : DrawingApp.MDrawing) =
        let texture = 
            m.mfilename |> Mod.map (fun path -> 
                let pi = PixTexture2d(PixImageMipMap([|PixImage.Create(path)|]),true)
                pi :> ITexture
            )

        Quad (Quad3d [| V3d(0,-2,-2); V3d(0,-2,2); V3d(0,2,2); V3d(0,2,-2) |]) 
            |> Scene.render [on Mouse.move MoveCursor; on (Mouse.down' MouseButtons.Left) AddPoint]
            |> (Scene.textured texture) :> ISg<_>
            |> Scene.effect [
                    toEffect DefaultSurfaces.trafo;
                    toEffect DefaultSurfaces.vertexColor;
                    toEffect DefaultSurfaces.diffuseTexture]
                  //  toEffect DefaultSurfaces.simpleLighting]
        
    let viewScene (sizes : IMod<V2i>) (m : DrawingApp.MDrawing) =        
        let cameraView = CameraView.lookAt (V3d.IOO * 5.0) V3d.OOO V3d.OOI |> Mod.constant
        let frustum = sizes |> Mod.map (fun (b : V2i) -> Frustum.perspective 60.0 0.1 10.0 (float b.X / float b.Y))        
        [viewDrawing m 
         viewQuad    m]
            |> Scene.group
            |> Scene.camera (Mod.map2 Camera.create cameraView frustum)           

    let subscriptions (m : DrawingApp.Drawing) =
        Many [Input.key Down Keys.Enter (fun _ _-> ClosePolygon)
              Input.key Down Keys.Left  (fun _ _-> Undo)
              Input.key Down Keys.Right (fun _ _-> Redo)
              
              Input.toggleKey Keys.LeftCtrl (fun _ -> PickStart) (fun _ -> PickStop)

              Input.key Down Keys.D1  (fun _ _-> ChangeStyle 0)
              Input.key Down Keys.D2  (fun _ _-> ChangeStyle 1)
              Input.key Down Keys.D3  (fun _ _-> ChangeStyle 2)
              Input.key Down Keys.D4  (fun _ _-> ChangeStyle 3)

              ]

    let (initial : DrawingApp.Drawing) = { 
            finished = PSet.empty
            working = None
            _id = null
            history = None; future = None
            picking = None 
            filename = @"C:\Users\BOOM\Desktop\P1010819.jpg"
            style = styles.[0]}

    let app s =
        {
            initial = initial
            update = update (None)
            view = viewScene s
            ofPickMsg = fun _ _ -> []
            subscriptions = subscriptions
        }


