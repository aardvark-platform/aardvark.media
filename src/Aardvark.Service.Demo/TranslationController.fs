namespace DragNDrop

module TranslateController =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI
    open Aardvark.UI.Primitives

    open Aardvark.Base.Geometry
    open TrafoController

    open DragNDrop

    type RayPart with
        member x.Transformed(t : Trafo3d) =
            RayPart(FastRay3d(x.Ray.Ray.Transformed(t.Forward)), x.TMin, x.TMax)    

    [<AutoOpen>]
    module Config =
        let coneHeight     = 0.1
        let coneRadius     = 0.03
        let cylinderRadius = 0.015
        let tessellation   = 8    
        
    let closestT (r : RayPart) (axis : Axis) =
        let other =
            match axis with
            | X -> Ray3d(V3d.OOO, V3d.IOO)
            | Y -> Ray3d(V3d.OOO, V3d.OIO)
            | Z -> Ray3d(V3d.OOO, V3d.OOI)

        let mutable unused = 0.0
        let mutable t = 0.0
        r.Ray.Ray.GetMinimalDistanceTo(other,&unused,&t) |> ignore
        t

    let updateController (m : Transformation) (a : TrafoController.Action) =
        match a with
            | Hover axis -> 
                { m with hovered = Some axis }
            | Unhover -> 
                { m with hovered = None }
            | Grab (rp, axis) ->

                //let pivot = 
                //    match m.mode with
                //      | TrafoMode.Global -> m.currentTrafo
                //      | TrafoMode.Local | _ -> Trafo3d.Identity

                let offset = closestT rp axis
                { m with grabbed = Some { offset = offset; axis = axis; hit = V3d.NaN }; }
            | Release ->
                match m.grabbed with
                | Some _ ->                     
                    let trans = Trafo3d.Translation m.workingPose.position

                    let rot = m.fullPose |> Pose.toRotTrafo
                    let newPos = rot.Forward.TransformPos m.workingPose.position

                    let pose = { m.fullPose with position = m.fullPose.position + newPos }

                    { m with grabbed = None; fullTrafo = trans * m.fullTrafo; workingPose = Pose.identity; fullPose = pose }
                | None   -> m
            | MoveRay rp ->
                match m.grabbed with
                | Some { offset = offset; axis = axis } ->

                     let other =
                        match axis with
                        | X -> V3d.IOO
                        | Y -> V3d.OIO
                        | Z -> V3d.OOI

                     // implement mode switch here (t2)
                     
                     let closestPoint = closestT rp axis
                     let shift = (closestPoint - offset) * other
                     //let trafo = Trafo3d.Translation ((closestPoint - offset) * other)

                     { m with workingPose = { m.workingPose with position = shift } }
                | None -> m
            | SetMode a->
                m    

    let viewController (liftMessage : TrafoController.Action -> 'msg) (m : MTransformation) =
        
        let arrow rot axis =
            let col =
                m.hovered |> Mod.map2 (colorMatch axis) (m.grabbed |> Mod.map (Option.map ( fun p -> p.axis )))

            Sg.cylinder tessellation col (Mod.constant cylinderRadius) (Mod.constant 1.0) 
            |> Sg.noEvents
            |> Sg.andAlso (                
                IndexedGeometryPrimitives.solidCone V3d.OOI V3d.OOI coneHeight coneRadius tessellation C4b.Red 
                 |> Sg.ofIndexedGeometry 
                 |> Sg.noEvents
                )
            |> Sg.pickable (Cylinder3d(V3d.OOO,V3d.OOI + V3d(0.0,0.0,0.1),cylinderRadius + 0.1) |> PickShape.Cylinder)
            |> Sg.transform rot       
            |> Sg.uniform "HoverColor" col
            |> Sg.trafo (m.workingPose |> Mod.map Pose.trafoWoScale)
            |> Sg.withEvents [ 
                    Sg.onEnter        (fun _ ->   Hover axis)
                    Sg.onMouseDownEvt (fun evt -> Grab (evt.localRay, axis))
                    Sg.onLeave        (fun _ ->   Unhover) 
               ]
            |> Sg.Incremental.withGlobalEvents ( 
                    amap {
                        let! grabbed = m.grabbed
                        if grabbed.IsSome then
                            yield Global.onMouseMove (fun e -> MoveRay e.localRay)
                            yield Global.onMouseUp   (fun _ -> Release)
                    }
                )

        let arrowX = arrow (Trafo3d.RotationY Constant.PiHalf) X
        let arrowY = arrow (Trafo3d.RotationX -Constant.PiHalf) Y
        let arrowZ = arrow (Trafo3d.RotationY 0.0) Z
                
        Sg.ofList [arrowX; arrowY; arrowZ ]
        |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; Shader.hoverColor |> toEffect; DefaultSurfaces.simpleLighting |> toEffect]        
        |> Sg.trafo (m.fullPose |> Mod.map Pose.trafoWoScale)//|> Sg.trafo (m.fullPose |> Mod.map Pose.trafoWoScale) //(m.fullPose |> Mod.map Pose.toTrafo)
        |> Sg.map liftMessage            
