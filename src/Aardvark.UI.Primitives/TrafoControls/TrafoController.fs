namespace Aardvark.UI.Trafos

open Aardvark.Base.Incremental
open Aardvark.Base

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Axis = 
    let toV3d axis =
      match axis with 
        | X -> V3d.XAxis
        | Y -> V3d.YAxis
        | Z -> V3d.ZAxis

    let toCircle r axis =        
        Circle3d(V3d.Zero, (axis |> toV3d), r)


     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]      
module Pose =
    let identity = { position = V3d.Zero; rotation = Rot3d.Identity; scale = V3d.III }
    let inline trafo (p : Pose) = p.Trafo
    let translate p = { position = p; rotation = Rot3d.Identity; scale = V3d.III }
    let toRotTrafo x = 
        Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse)
    let toTrafo x = 
        let rot = x |> toRotTrafo
        Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position
    let trafoWoScale x = 
        (x |> toRotTrafo) * Trafo3d.Translation x.position
    let toTranslateTrafo x =
        Trafo3d.Translation x.position

    let transform (p : Pose) (t : Trafo3d) = 
        let newRot = Rot3d.FromFrame(t.Forward.C0.XYZ,t.Forward.C1.XYZ,t.Forward.C2.XYZ)
        { position = t.Forward.TransformPosProj p.position; rotation = newRot; scale = t.Forward.TransformPos V3d.III } // wrong


module TrafoController = 
    open Aardvark.Base
    open Aardvark.Base.Geometry
    open Aardvark.Base.Incremental

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
        | Nop

    let colorMatch axis = 
        fun g h ->
            match h, g, axis with
            | _,      Some g, p when g = p -> C4b.Yellow
            | Some h, None,   p when h = p -> C4b.White
            | _,      _,      X -> C4b.Red
            | _,      _,      Y -> C4b.Green
            | _,      _,      Z -> C4b.Blue

    let pickingTrafo (m:MTransformation) : IMod<Trafo3d> =
        adaptive {
            let! mode = m.mode
            match mode with
                | TrafoMode.Local -> 
                    return! m.pose |> Mod.map Pose.toTrafo
                | TrafoMode.Global -> 
                    let! a = m.pose
                    return Trafo3d.Translation(a.position)
                | _ -> 
                    return failwith ""
        }

module Sg =
    open Aardvark.Base
    open Aardvark.Base.Incremental

    let computeInvariantScale (view : IMod<CameraView>) (near : IMod<float>) (p:IMod<V3d>) (size:IMod<float>) (hfov:IMod<float>) =
        adaptive {
            let! p = p
            let! v = view
            let! near = near
            let! size = size
            let! hfov = hfov
            let hfov_rad = Conversion.RadiansFromDegrees(hfov)
               
            let wz = Fun.Tan(hfov_rad / 2.0) * near * size
            let dist = V3d.Distance(p, v.Location)

            return ( wz / near ) * dist
        }


module Shader =
    
    open FShade
    open Aardvark.Base
    open Aardvark.Base.Rendering.Effects

    let hoverColor (v : Vertex) =
        vertex {
            let c : V4d = uniform?HoverColor
            return { v with c = c }
        }