namespace DragNDrop

module RotationController =
    
    // old version ...https://github.com/aardvark-platform/aardvark.media/blob/base2/src/Aardvark.ImmutableSceneGraph/TrafoApps.fs 

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.Base.Rendering
    open Aardvark.Base.Geometry
   
    open Aardvark.SceneGraph    

    open Aardvark.UI
    open Aardvark.UI.Primitives


    type RayPart with
        member x.Transformed(t : Trafo3d) =
            RayPart(FastRay3d(x.Ray.Ray.Transformed(t.Forward)), x.TMin, x.TMax)

    [<AutoOpen>]
    module Config =
        let cylinderRadius = 0.015
        let radius    = 1.0
        let tesselation = 32.0    

    type ControllerAction = 
      | Hover of Axis
      | Unhover 
      | RotateRay of RayPart
      | Grab of RayPart * Axis
      | Release
      | Nop
    
    module RotationHandle = 
        
        let circlePoint (c:V3d) r ang axis =
            let x = c.X + r * cos ang
            let y = c.Y + r * sin ang
            match axis with
              | X -> V3d(0.0, x, y)
              | Y -> V3d(x, 0.0, y)
              | Z -> V3d(x, y, 0.0)
        
        let PI2 = System.Math.PI * 2.0

        let circle c r tess axis =
            let step = PI2 / tess            
            [0.0 .. step .. (PI2)] |> List.map (fun x -> circlePoint c r x axis) |> List.toSeq

        let sg axis =
            [
                let poly = (circle V3d.Zero radius tesselation axis) |> Polygon3d
                
                let segments = 
                    poly.EdgeLines 
                      |> Seq.map(fun edge -> Line3d(edge.P0, edge.P1)) 
                      |> Seq.toArray                      
                
                yield Sg.lines (Mod.constant(C4b.Red)) (Mod.constant(segments))
            ] |> Sg.group
     
    let axisToV3d axis = 
        match axis with 
          | X -> V3d.XAxis
          | Y -> V3d.YAxis
          | Z -> V3d.ZAxis

    let toCircle axis = 
        let v = axis |> axisToV3d        
        Circle3d(V3d.Zero, v, radius)        
          
    let intersect (ray : RayPart) (circle:Circle3d) =
        let mutable p = V3d.NaN
        let mutable t = 0.0  
            
        ray.Ray.Ray.Intersects(circle.Plane, &t, &p), p    

    let updateController (m : Transformation) (a : ControllerAction) =
        match a with
            | Hover axis -> 
                { m with hovered = Some axis }
            | Unhover -> 
                { m with hovered = None }
            | Grab (rp, axis) ->
                Log.warn "grabbing %A" axis
                let _, p = intersect rp (axis |> toCircle)

                //set pivot
                
                { m with grabbed = Some { offset = 0.0; axis = axis; hit = p }; workingPose = Pose.identity }
            | Release ->
                match m.grabbed with
                    | Some _ ->   
//                       let rot = m.workingPose |> Pose.toRotTrafo
//                       let p = { m.pose with rotation = m.pose.rotation * m.workingPose.rotation }
//
//                       { m with grabbed = None; workingPose = Pose.identity; pose = p 
                         let pose,preview =
                            match m.mode with
                                | TrafoMode.Global ->
                                     let result = { m.pose with rotation =  m.workingPose.rotation * m.pose.rotation  }
                                     result, Pose.trafo result
                                | TrafoMode.Local -> 
                                    let result = { m.pose with rotation = m.pose.rotation * m.workingPose.rotation } 
                                    result, Pose.toTrafo result
                                | _ -> failwith ""

                         { m with pose = pose;  previewTrafo = preview; grabbed = None; workingPose = Pose.identity }
                    | _ -> m
            | RotateRay rp ->
                match m.grabbed with
                | Some { offset = offset; axis = axis; hit = hit } ->
                     let h, p = intersect rp (axis |> toCircle)
                     if h && (not hit.IsNaN) then                         
                         let rotation = Rot3d(hit.Normalized, p.Normalized)
                         let workingPose = { m.workingPose with rotation = rotation } 
                         let _,preview =
                            match m.mode with
                                | TrafoMode.Global ->
                                     let result = { m.pose with rotation =  workingPose.rotation * m.pose.rotation  }
                                     result, Pose.trafo result
                                | TrafoMode.Local -> 
                                    let result = { m.pose with rotation = m.pose.rotation * workingPose.rotation } 
                                    result, Pose.toTrafo result
                                | _ -> failwith ""
                         { m with workingPose = workingPose; previewTrafo = preview }
                     else 
                        m
                | None -> m
            | Nop -> m

    let viewController (liftMessage : ControllerAction -> 'msg) (m : MTransformation) : ISg<'msg> =
            
        let circle dir axis =
            let col =
                m.hovered |> Mod.map2 ( fun g h ->
                 
                 match h, g, axis with
                 | _,      Some g, p when g = p -> C4b.Yellow
                 | Some h, None,   p when h = p -> C4b.White
                 | _,      _,      X -> C4b.Red
                 | _,      _,      Y -> C4b.Green
                 | _,      _,      Z -> C4b.Blue
                ) (m.grabbed |> Mod.map (Option.map ( fun p -> p.axis )))                 
            
            let circle = Circle3d(V3d.Zero, dir, radius)
               
            RotationHandle.sg axis
            |> Sg.noEvents
            |> Sg.uniform "HoverColor" col
            |> Sg.uniform "LineWidth" (Mod.constant(cylinderRadius * 20.0))
            

            
        let pickOnCircle dir axis =
            let circle = Circle3d(V3d.Zero, dir, radius)        
            Sg.empty 
                |> Sg.Incremental.withGlobalEvents ( 
                    amap {                        
                        let! grabbed = m.grabbed
                        if grabbed.IsSome then
                            yield Global.onMouseMove (fun e -> RotateRay e.localRay)
                            yield Global.onMouseUp   (fun _ -> Release)
                    }
                   )
                |> Sg.map liftMessage

        let hovering dir axis  =
            let circle = Circle3d(V3d.Zero, dir, radius)   
            Sg.empty 
            |> Sg.withGlobalEvents [
                    Global.onMouseMove (fun e -> 
                            let hit, p = intersect e.localRay circle
                            if hit then
                                let dist = (V3d.Distance(V3d.Zero, p))
                                if (dist > radius - 0.1 && dist < radius + 0.1) then
                                    Hover axis
                                else                                            
                                    m.hovered 
                                        |> Mod.map(fun x ->
                                        match x with
                                            | Some h when h = axis -> Unhover
                                            | _ -> Nop) |> Mod.force
                            else
                                Nop
                        );
                    Global.onMouseDown (fun e ->
                        m.hovered |> Mod.map(fun x ->
                            match x with
                                | Some h when h = axis -> 
                                    Grab (e.localRay, axis)
                                | _ -> Nop
                        ) |> Mod.force
                    )
                ]
            |> Sg.map liftMessage

        let currentTrafo : IMod<Trafo3d> =
            adaptive {
                let! mode = m.mode
                match mode with
                    | TrafoMode.Local -> 
                        return! m.previewTrafo
                    | TrafoMode.Global -> 
                        let! a = m.previewTrafo
                        return Trafo3d.Translation(a.Forward.TransformPos(V3d.Zero))
            }

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

        let pickGraphs =
            [
                for (v,a) in [V3d.XAxis, Axis.X; V3d.YAxis, Axis.Y; V3d.ZAxis, Axis.Z] do
                    yield pickOnCircle v a  |> Sg.trafo controller2
                    yield hovering v a |> Sg.trafo currentTrafo
            ] |> Sg.ofSeq 
        
        let scene =
            Sg.ofList [circle V3d.XAxis Axis.X; circle V3d.YAxis Axis.Y; circle V3d.ZAxis Axis.Z ]
            |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; Shader.hoverColor |> toEffect] //; DefaultSurfaces.simpleLighting |> toEffect        
            |> Sg.trafo currentTrafo
            |> Sg.noEvents        
            |> Sg.map liftMessage   
        
        Sg.ofSeq [pickGraphs; scene]                            