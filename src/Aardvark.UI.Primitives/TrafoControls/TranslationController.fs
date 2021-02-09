namespace Aardvark.UI.Trafos

module TranslateController =

    open Aardvark.Base
    open FSharp.Data.Adaptive
    
    open Aardvark.SceneGraph
    open Aardvark.Rendering
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
                { m with grabbed = Some { offset = offset; axis = axis; hit = V3d.NaN; }; workingPose = Pose.identity }
            | Release ->
                match m.grabbed with
                | Some _ ->                     
                    let resultPose,preview =
                        match m.mode with
                            | TrafoMode.Global ->
                                 let result = { m.pose with position = m.pose.position + m.workingPose.position }
                                 result, Pose.trafo result
                            | TrafoMode.Local -> 
                                let rot = m.pose |> Pose.toRotTrafo
                                let newPos = rot.Forward.TransformPos m.workingPose.position
                                let result = { m.pose with position = m.pose.position + newPos } 
                                result, Pose.toTrafo result
                            | _ -> failwith ""

                    { m with grabbed = None; workingPose = Pose.identity; pose = resultPose; previewTrafo = preview }
                | None   -> m
            | MoveRay rp ->
                match m.grabbed with
                | Some { offset = offset; axis = axis } ->

                     let other =
                        match axis with
                        | X -> V3d.IOO
                        | Y -> V3d.OIO
                        | Z -> V3d.OOI                     

                     let closestPoint = closestT rp axis
                     let shift = (closestPoint - offset) * other         

                     let workingPose = { m.workingPose with position = shift }

                     let preview =
                        match m.mode with
                            | TrafoMode.Local -> 
                                Pose.toTrafo workingPose * Pose.toTrafo m.pose
                            | TrafoMode.Global -> 
                                Pose.toTrafo m.pose * Pose.toTrafo workingPose // bug?
                            | _ -> failwith ""

                     { m with workingPose = workingPose; previewTrafo = preview }
                | None -> m
            | SetMode a-> m 
            | Nop -> m

    let viewController (liftMessage : TrafoController.Action -> 'msg) (v : aval<CameraView>) (m : AdaptiveTransformation) =
        
        let arrow rot axis =
            let col =
                let g : aval<Option<PickPoint>> = m.grabbed
                let p : aval<Option<Axis>> =  (g |> AVal.map (Option.map ( fun (p:PickPoint) -> p.axis )))
                m.hovered |> AVal.map2 (colorMatch axis) p

            Sg.cylinder tessellation col (AVal.constant cylinderRadius) (AVal.constant 1.0) 
            |> Sg.noEvents
            |> Sg.andAlso (                
                IndexedGeometryPrimitives.solidCone V3d.OOI V3d.OOI coneHeight coneRadius tessellation C4b.Red 
                 |> Sg.ofIndexedGeometry 
                 |> Sg.noEvents
                )
            |> Sg.pickable (Cylinder3d(V3d.OOO,V3d.OOI + V3d(0.0,0.0,0.1),cylinderRadius + 0.1) |> PickShape.Cylinder)
            |> Sg.transform rot       
            |> Sg.uniform "HoverColor" col           
            |> Sg.withEvents [ 
                    Sg.onEnter        (fun _ ->   Hover axis)
                    Sg.onMouseDownEvt (fun evt -> Grab (evt.localRay, axis))
                    Sg.onLeave        (fun _ ->   Unhover) 
               ]
               
        let scaleTrafo pos =            
            Sg.computeInvariantScale' v (AVal.constant 0.1) pos (AVal.constant 0.3) (AVal.constant 60.0) |> AVal.map Trafo3d.Scale

        let pickGraph =
            Sg.empty 
                |> Sg.Incremental.withGlobalEvents ( 
                        amap {
                            let! grabbed = m.grabbed
                            if grabbed.IsSome then
                                yield Global.onMouseMove (fun e -> MoveRay e.localRay)
                                yield Global.onMouseUp   (fun _ -> Release)
                        }
                    )                
                |> Sg.trafo (m.pose |> Pose.toTrafo' |> TrafoController.getTranslation |> scaleTrafo)
                |> Sg.trafo (TrafoController.pickingTrafo m)
                |> Sg.map liftMessage

        let arrowX = arrow (Trafo3d.RotationY Constant.PiHalf)  Axis.X
        let arrowY = arrow (Trafo3d.RotationX -Constant.PiHalf) Axis.Y
        let arrowZ = arrow (Trafo3d.RotationY 0.0)              Axis.Z
          
        let currentTrafo : aval<Trafo3d> =
            adaptive {
                let! mode = m.mode
                match mode with
                    | TrafoMode.Local -> 
                        return! m.previewTrafo
                    | TrafoMode.Global -> 
                        let! a = m.previewTrafo
                        return Trafo3d.Translation(a.Forward.TransformPos(V3d.Zero))
                    | _ -> 
                        return failwith ""
            }

        let scene =      
            Sg.ofList [arrowX; arrowY; arrowZ ]
            |> Sg.effect [ Shader.stableTrafo |> toEffect; Shader.hoverColor |> toEffect]
            |> Sg.trafo (currentTrafo |> TrafoController.getTranslation |> scaleTrafo)
            |> Sg.trafo currentTrafo            
            |> Sg.map liftMessage   
        
        Sg.ofList [pickGraph; scene]         
