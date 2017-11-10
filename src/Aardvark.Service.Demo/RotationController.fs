namespace DragNDrop

module RotationController =
    
    // old version ...https://github.com/aardvark-platform/aardvark.media/blob/base2/src/Aardvark.ImmutableSceneGraph/TrafoApps.fs 

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.Base.Rendering
    open Aardvark.Base.Geometry
   
    open Aardvark.UI
    open Aardvark.UI.Primitives

    open Aardvark.SceneGraph    

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
            //[0.0 .. 1.0 .. 1.0] |> List.map (fun x -> circlePoint c r x axis) |> List.toSeq
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
                
                { m with grabbed = Some { offset = 0.0; axis = axis; hit = p } }
            | Release ->
                match m.grabbed with
                    | Some _ -> 
                                            
                       let rot = m.workingPose |> Pose.toRotTrafo
                       let p = { m.fullPose with rotation = m.fullPose.rotation * m.workingPose.rotation }

                       let t1 = p |> Pose.toTrafo
                       let t2 = rot * m.fullTrafo

                       Log.line "\n%A\n%A\n" t1.Forward t2.Forward

                       { m with grabbed = None; workingPose = Pose.identity; fullTrafo = rot * m.fullTrafo; fullPose = p }
                    | _ -> m
            | RotateRay rp ->
                match m.grabbed with
                | Some { offset = offset; axis = axis; hit = hit } ->
                     let h, p = intersect rp (axis |> toCircle)
                     if h && (not hit.IsNaN) then                         
                         let rotation = Rot3d(hit.Normalized, p.Normalized)
                         { m with workingPose = { m.workingPose with rotation = rotation } }
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
            |> Sg.uniform "HoverColor" col
            |> Sg.uniform "LineWidth" (Mod.constant(cylinderRadius * 20.0))
            |> Sg.trafo (m.workingPose |> Mod.map Pose.trafoWoScale) // scale hate
            |> Sg.noEvents            
            |> Sg.Incremental.withGlobalEvents ( 
                    amap {                        
                        let! grabbed = m.grabbed
                        if grabbed.IsSome then
                            yield Global.onMouseMove (fun e -> RotateRay e.localRay)
                            yield Global.onMouseUp   (fun _ -> Release)
                        else
                            yield Global.onMouseMove (
                                fun e -> 
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
                                        Nop)
                            yield Global.onMouseDown (
                                fun e ->
                                    m.hovered |> Mod.map(fun x ->
                                        match x with
                                          | Some h when h = axis -> 
                                                Grab (e.localRay, axis)
                                          | _ -> Nop
                                    ) |> Mod.force

                                    //torus.GetMinimalDistance
                                    //check enter -> hover or unhover
                                    //on click -> grab
                                    )
                    }
                )

        let circleX = circle V3d.XAxis Axis.X
        let circleY = circle V3d.YAxis Axis.Y
        let circleZ = circle V3d.ZAxis Axis.Z
        
        Sg.ofList [circleX; circleY; circleZ ]
        |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; Shader.hoverColor |> toEffect] //; DefaultSurfaces.simpleLighting |> toEffect        
        |> Sg.trafo (m.fullPose |> Mod.map Pose.trafoWoScale) //(m.fullTrafo)//
        |> Sg.noEvents        
        |> Aardvark.UI.``F# Sg``.Sg.map liftMessage                               