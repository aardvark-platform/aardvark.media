namespace Aardvark.UI.Trafos

module ScaleController =

    open Aardvark.Base
    open Aardvark.Base.Incremental    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.Base.Geometry
    open Aardvark.UI    
    
    open TrafoController

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
                let offset = closestT rp axis
                { m with grabbed = Some { offset = offset; axis = axis; hit = V3d.NaN } } 
            | Release ->                
                match m.grabbed with
                  | Some _ ->                     
                    let scale = Trafo3d.Scale m.workingPose.scale
                    let p = { m.pose with scale = m.workingPose.scale }
                    { m with grabbed = None; pose = p; workingPose = Pose.identity }
                  | None   -> m
            | MoveRay rp ->
                match m.grabbed with
                | Some { offset = offset; axis = axis } ->

                    let closestPoint = closestT rp axis 
                    let drag = (closestPoint - (offset)).Clamp(-1.0, System.Double.MaxValue)

                    let scale =
                        match axis with
                        | X -> V3d.IOO * drag + V3d.One
                        | Y -> V3d.OIO * drag + V3d.One
                        | Z -> V3d.OOI * drag + V3d.One                    

                    { m with workingPose = { m.workingPose with scale = scale }}
                | None -> m
            | Nop -> m
            | SetMode _ -> m

    

    let viewController (liftMessage : TrafoController.Action -> 'msg) (scaling : IMod<V3d> -> IMod<float>) (m : MTransformation) =
                
        let box = Sg.box' C4b.Red (Box3d.FromCenterAndSize(V3d.OOI, V3d(coneHeight)))

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
            |> Sg.andAlso (box |> Sg.noEvents)
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

        let arrowX = arrow (Trafo3d.RotationY Constant.PiHalf)  Axis.X
        let arrowY = arrow (Trafo3d.RotationX -Constant.PiHalf) Axis.Y
        let arrowZ = arrow (Trafo3d.RotationY 0.0)              Axis.Z
                
        Sg.ofList [arrowX; arrowY; arrowZ ]
        |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; Shader.hoverColor |> toEffect; DefaultSurfaces.simpleLighting |> toEffect]        
        |> Sg.trafo (m.pose |> Mod.map Pose.trafoWoScale)
        |> Sg.map liftMessage               