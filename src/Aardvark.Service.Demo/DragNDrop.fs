namespace DragNDrop

module App =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    open Aardvark.SceneGraph
    open Aardvark.Base.Rendering
    open Aardvark.UI
    open Aardvark.UI.Primitives
    open Aardvark.Base.Geometry

    type Action = 
        | StartDrag of SceneHit
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
                { m with dragging = Some { PickPoint = p.globalPosition; Offset =  p.globalPosition - m.trafo.Forward.TransformPos(V3d.OOO) }}
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
                attribute "style" "width:100%; height: 100%"
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

module Matrix = 
    open Aardvark.Base
    open Aardvark.Base.Incremental

    let decomp (m:M44d) = 

        let t = m.C3.XYZ

        let sx = m.C0.XYZ.Length
        let sy = m.C1.XYZ.Length
        let sz = m.C2.XYZ.Length

        let s = V3d(sx, sy, sz)

        let rc0 = m.C0.XYZ / s.X
        let rc1 = m.C1.XYZ / s.Y
        let rc2 = m.C2.XYZ / s.Z

        let r : M33d = M33d.FromCols(rc0, rc1, rc2)        

        s,r,t

    //let expandRot (m:M33d) =
    //    M44d.

    let decomp' (t:Trafo3d) = 

        let fs,fr,ft = decomp t.Forward
        let _, br,_ = decomp t.Backward

        let s = Trafo3d.Scale fs
        let t = Trafo3d.Translation ft

        let a = fr |> Rot3d.FromM33d |> M44d.Rotation
        let b = br |> Rot3d.FromM33d |> M44d.Rotation
                       
        s, Trafo3d(a, b), t

    let filterTrafo (mode : IMod<TrafoMode>) (trafo : IMod<Trafo3d>)=
        adaptive {
            let! tr = trafo
            let! m = mode
            let t = 
                match m with
                  | TrafoMode.Global -> Trafo3d.Translation(tr.Forward.C3.XYZ)
                  | TrafoMode.Local | _ -> tr
                 
            return  t
        }

module TrafoController = 
    open Aardvark.Base
    open Aardvark.Base.Geometry

    let initial =
        { 
            hovered      = None
            grabbed      = None
            mode         = TrafoMode.Global
            workingPose  = Pose.identity
            pose         = Pose.identity
            previewTrafo = Trafo3d.Identity
        }


    type Action = 
        | Hover   of Axis
        | Unhover 
        | MoveRay of RayPart
        | Grab    of RayPart * Axis
        | Release
        | SetMode of TrafoMode

    let colorMatch axis = 
        fun g h ->
            match h, g, axis with
            | _,      Some g, p when g = p -> C4b.Yellow
            | Some h, None,   p when h = p -> C4b.White
            | _,      _,      X -> C4b.Red
            | _,      _,      Y -> C4b.Green
            | _,      _,      Z -> C4b.Blue

module Shader =
    
    open FShade
    open Aardvark.Base
    open Aardvark.Base.Rendering.Effects

    let hoverColor (v : Vertex) =
        vertex {
            let c : V4d = uniform?HoverColor
            return { v with c = c }
        }




