namespace DragNDrop

module App =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI
    open Aardvark.Base.Geometry

    type Action = 
        | StartDrag of SceneEvent
        | StopDrag
        | MoveRay      of RayPart 
        | CameraAction of CameraController.Message

    let update (m : Model) (a : Action) =

        match a with
            | CameraAction a when Option.isNone m.dragging -> 
                // disable cam on dragging
                { m with camera = CameraController.update m.camera a }
            | MoveRay p ->
                match m.dragging with
                    | Some { PickPoint = worldSpaceStart; Offset = centerOffset } -> 
                        let i = p.Ray.Ray.Intersect (Plane3d(V3d.OOI,worldSpaceStart))
                        { m with trafo = Trafo3d.Translation (i - centerOffset) }
                    | None -> m
            | StartDrag p -> 
                { m with dragging = Some { PickPoint = p.position; Offset =  p.position - m.trafo.Forward.TransformPos(V3d.OOO) }}
            | StopDrag    -> { m with dragging = None   }
            | _ -> m


    let scene (m : MModel) =

        let box =
            Sg.box (Mod.constant C4b.Green) (Mod.constant (Box3d.FromCenterAndSize(V3d.OOO,V3d.III)))
            |> Sg.requirePicking
            |> Sg.noEvents    
            |> Sg.withEvents [
                    Sg.onMouseDownEvt (fun e -> StartDrag e )
                    Sg.onMouseUp      (fun p -> StopDrag    )
               ]
            |> Sg.trafo m.trafo

        let plane = 
            Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.OOO,V3d(10.0,10.0,0.1)))
            |> Sg.noEvents

        let scene = 
            Sg.ofSeq [box; plane]
            |> Sg.effect [
                    toEffect <| DefaultSurfaces.trafo
                    toEffect <| DefaultSurfaces.vertexColor
                    toEffect <| DefaultSurfaces.simpleLighting
               ]

            

        CameraController.controlledControl m.camera CameraAction (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
            (AttributeMap.ofList [ 
                attribute "style" "width:100%; height: 100%"; RenderControl.onMouseMove (fun r t -> MoveRay r)
             ]) scene

    let view (m : MModel) =
        div [] [
            scene m
        ]

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun (model : Model) -> CameraController.threads model.camera |> ThreadPool.map CameraAction
            initial = { trafo = Trafo3d.Identity; dragging = None; camera = CameraController.initial }
            update = update
            view = view
        }

    let start() = App.start app



module TranslateController =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI
    open Aardvark.Base.Geometry

    open DragNDrop
    open Aardvark.Rendering.GL.ContextHandle

    type Action = 
        | Hover of Axis
        | Unhover 
        | MoveRay of RayPart
        | Grab of V3d * Axis
        | Release
        | CameraAction of CameraController.Message

    let updateTransformation (m : Transformation) (a : Action) =
        match a with
            | Hover axis -> 
                { m with hovered = Some axis }
            | Unhover -> 
                { m with hovered = None }
            | Grab (point, axis) ->
                printfn "%A" point
                let offset = 
                    let center = V3d.OOO |> m.trafo.Forward.TransformPos 
                    point - center
                { m with grabbed = Some { point = point; offset = offset; axis = axis } } 
            | Release ->
                { m with grabbed = None }
            | MoveRay rp ->
                match m.grabbed with
                | Some { point = point; offset = offset; axis = axis } ->
                    let other =
                        match axis with
                        | X -> Ray3d(point, V3d.IOO)
                        | Y -> Ray3d(point, V3d.OIO)
                        | Z -> Ray3d(point, V3d.OOI)

                    let nearest = rp.Ray.Ray.GetClosestPointOn other

                    let trafo = Trafo3d.Translation (nearest - offset)

                    { m with trafo = trafo }
                | None -> m
            | _ -> m

    let update (m : Scene) (a : Action) =
        match a with
            | CameraAction a when m.transformation.grabbed.IsNone -> 
                { m with camera = CameraController.update m.camera a }
            | _ -> { m with transformation = updateTransformation m.transformation a }

    open FShade
    open Aardvark.Base.Rendering.Effects

    let color (v : Vertex) =
        vertex {
            let c : V4d = uniform?SuperColor
            return { v with c = c }
        }

    let viewController (m : MTransformation) =

        let coneRadius = 0.1
        let cylinderRadius = 0.05
        let tessellation = 8
        
        let arrow rot axis =
            let col =
                m.hovered |> Mod.map2 ( fun g h ->
                 match h, g, axis with
                 | _,      Some g, p when g = p -> C4b.Yellow
                 | Some h, None,   p when h = p -> C4b.White
                 | _,      _,      X -> C4b.Red
                 | _,      _,      Y -> C4b.Green
                 | _,      _,      Z -> C4b.Blue
                ) (m.grabbed |> Mod.map (Option.map ( fun p -> p.axis )))
            Sg.cylinder tessellation col (Mod.constant cylinderRadius) (Mod.constant 1.0) 
            |> Sg.noEvents
            |> Sg.andAlso (                
                IndexedGeometryPrimitives.solidCone 
                    V3d.OOI 
                    V3d.OOI 
                    0.1 
                    coneRadius 
                    tessellation 
                    C4b.Red 
                 |> Sg.ofIndexedGeometry 
                 |> Sg.noEvents
                )
            |> Sg.pickable (Cylinder3d(V3d.OOO,V3d.OOI + V3d(0.0,0.0,0.1),cylinderRadius + 0.1) |> PickShape.Cylinder)
            |> Sg.transform rot
            |> Sg.uniform "SuperColor" col
            |> Sg.withEvents [ 
                                Sg.onEnter (fun _ -> Hover axis)
                                Sg.onMouseDown (fun _ p -> Grab (p,axis))
                                Sg.onLeave (fun _ -> Unhover) 
                             ]

        let arrowX = arrow (Trafo3d.RotationY Constant.PiHalf) X
        let arrowY = arrow (Trafo3d.RotationX -Constant.PiHalf) Y
        let arrowZ = arrow (Trafo3d.RotationY 0.0) Z
        
        let ig =
            IndexedGeometryPrimitives.coordinateCross (V3d(10.0, 10.0, 10.0))
                |> Sg.ofIndexedGeometry
                |> Sg.effect [
                    toEffect DefaultSurfaces.trafo
                    toEffect DefaultSurfaces.vertexColor
                    ]
                |> Sg.noEvents
        Sg.ofList [arrowX; arrowY; arrowZ ]
        |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; color |> toEffect; DefaultSurfaces.simpleLighting |> toEffect]
        |> Sg.trafo m.trafo
        |> Sg.andAlso ig

    let viewScene (m : MScene) =
        CameraController.controlledControl m.camera CameraAction (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
            (AttributeMap.ofList [ 
                attribute "style" "width:100%; height: 100%"; 
                RenderControl.onMouseMove (fun r t -> MoveRay r)
                onMouseUp ( fun _ _ -> Release )
             ]) (viewController m.transformation)

    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]  

    let view (m : MScene) =
        require semui (
            Aardvark.UI.Html.SemUi.adornerMenu ["urdar", [text "asdfasdf"]] (viewScene m)
        )

    let app : App<Scene,MScene,Action>=
        {
            unpersist = Unpersist.instance
            threads = fun (model : Scene) -> CameraController.threads model.camera |> ThreadPool.map CameraAction
            initial = {  camera = CameraController.initial
                         transformation = 
                            {
                                hovered = None
                                grabbed = None
                                trafo = Trafo3d.Identity
                            }
                      }
            update = update
            view = view
        }

    let start() = App.start app