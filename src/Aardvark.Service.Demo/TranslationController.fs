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
                //      | TrafoMode.Global -> m.fullPose |> Pose.toTrafo
                //      | TrafoMode.Local | _ -> Trafo3d.Identity

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

                     // implement mode switch here (t2)

                     let closestPoint = closestT rp axis
                     let shift = (closestPoint - offset) * other
                     //let trafo = Trafo3d.Translation ((closestPoint - offset) * other)

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
            //|> Sg.trafo(m.pivotTrafo)
            //|> Sg.trafo (m.workingPose |> Mod.map Pose.trafoWoScale)
            //|> Sg.trafo(m.pivotTrafo |> Mod.map(fun x -> x.Inverse))
            |> Sg.withEvents [ 
                    Sg.onEnter        (fun _ ->   Hover axis)
                    Sg.onMouseDownEvt (fun evt -> Grab (evt.localRay, axis))
                    Sg.onLeave        (fun _ ->   Unhover) 
               ]

          
        let controller2 : IMod<Trafo3d> =
            adaptive {
                let! mode = m.mode
                match mode with
                    | TrafoMode.Local -> 
                        return! m.pose |> Mod.map Pose.toTrafo
                    | TrafoMode.Global -> 
                        let! a = m.pose
                        return Trafo3d.Translation(a.position)
            }

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
                |> Sg.trafo controller2
                |> Sg.map liftMessage

        let arrowX = arrow (Trafo3d.RotationY Constant.PiHalf) X
        let arrowY = arrow (Trafo3d.RotationX -Constant.PiHalf) Y
        let arrowZ = arrow (Trafo3d.RotationY 0.0) Z
          
        let controller : IMod<Trafo3d> =
            adaptive {
                let! mode = m.mode
                match mode with
                    | TrafoMode.Local -> 
                        return! m.previewTrafo
                    | TrafoMode.Global -> 
                        let! a = m.previewTrafo
                        return Trafo3d.Translation(a.Forward.TransformPos(V3d.Zero))
            }

        let scene =      
            Sg.ofList [arrowX; arrowY; arrowZ ]
            |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; Shader.hoverColor |> toEffect; DefaultSurfaces.simpleLighting |> toEffect]        
            |> Sg.trafo controller
            //|> Sg.trafo (m.fullPose |> Mod.map Pose.toRotTrafo)
            //|> Sg.trafo (m.pose |> Mod.map Pose.toTrafo)//|> Sg.trafo (m.fullPose |> Mod.map Pose.trafoWoScale) //(m.fullPose |> Mod.map Pose.toTrafo)
            |> Sg.map liftMessage   
        
        Sg.ofList [pickGraph; scene]         
