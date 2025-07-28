namespace Aardvark.UI.Trafos

open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.Rendering

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

    let toRotTrafo' (pose:aval<Pose>) : aval<Trafo3d> = 
        pose |> AVal.map(fun x -> Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse))

    let toTrafo x = 
        let rot = x |> toRotTrafo
        Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position

    let toTrafo' (pose : aval<Pose>) : aval<Trafo3d> =
        pose |> AVal.map(fun x ->
            let rot = x |> toRotTrafo
            Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position)        

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
    open FSharp.Data.Adaptive

    let initial =
        { 
            hovered      = None
            grabbed      = None
            mode         = TrafoMode.Global
            workingPose  = Pose.identity
            pose         = Pose.identity
            previewTrafo = Trafo3d.Identity
            scale        = 1.0
        }

    type Action = 
        | Hover   of Axis
        | Unhover 
        | MoveRay of RayPart
        | Grab    of RayPart * Axis
        | Release
        | SetMode of TrafoMode
        | Nop

    let colorMatch (axis : Axis) (g : Option<Axis>) (h : Option<Axis>) : C4b = 
        match h, g, axis with
        | _,      Some g, p when g = p -> C4b.Yellow
        | Some h, None,   p when h = p -> C4b.White
        | _,      _,      X -> C4b.Red
        | _,      _,      Y -> C4b.Green
        | _,      _,      Z -> C4b.Blue

    let pickingTrafo (m:AdaptiveTransformation) : aval<Trafo3d> =
        adaptive {
            let! mode = m.mode
            match mode with
                | TrafoMode.Local -> 
                    return! m.pose |> AVal.map Pose.toTrafo
                | TrafoMode.Global -> 
                    let! a = m.pose
                    return Trafo3d.Translation(a.position)
                | _ -> 
                    return failwith ""
        }

    let getTranslation (t : aval<Trafo3d>) =
        t |> AVal.map(fun x -> x.Forward.C3.XYZ)

module Sg =
    open Aardvark.Base
    open FSharp.Data.Adaptive

    let computeInvariantScale' (view : aval<CameraView>) (near : aval<float>) (p:aval<V3d>) (size:aval<float>) (hfov:aval<float>) =
        adaptive {
            let! p = p
            let! v = view
            let! near = near
            let! size = size
            let! hfov = hfov
            let hfov_rad = Conversion.RadiansFromDegrees(hfov)
               
            let wz = Fun.Tan(hfov_rad / 2.0) * near * size
            let dist = Vec.Distance(p,v.Location)

            return ( wz / near ) * dist
        }

    let computeInvariantScale (view : CameraView) (near : float) (p : V3d) (size : float) (hfov : float) =                    
        let hfov_rad = Conversion.RadiansFromDegrees(hfov)               
        let wz = Fun.Tan(hfov_rad / 2.0) * near * size
        let dist = Vec.Distance(p, view.Location)

        ( wz / near ) * dist
        


module Shader =
        
    open Aardvark.Base
    open Aardvark.Base.IO
    open Aardvark.Rendering
    open Aardvark.Rendering.Effects
    
    open FShade

    type Vertex =
        {
            [<Position>]                pos     : V4f
            [<WorldPosition>]           wp      : V4f
            [<TexCoord>]                tc      : V2f
            [<Color>]                   c       : V4f
            [<Normal>]                  n       : V3f
            [<Semantic("Scalar")>]      scalar  : float32
            [<Semantic("LightDir")>]    ldir    : V3f
        }

    let hoverColor (v : Vertex) =
        vertex {
            let c : V4f = uniform?HoverColor
            return { v with c = c }
        }

    [<ReflectedDefinition>]
    let transformNormal (n : V3f) =
        uniform.ModelViewTrafoInv.Transposed * V4f(n, 0.0f)
            |> Vec.xyz
            |> Vec.normalize

    let stableTrafo (v : Vertex) =
        vertex {
            let vp = uniform.ModelViewTrafo * v.pos
            let wp = uniform.ModelTrafo * v.pos
            return { 
                v with
                    pos  = uniform.ProjTrafo * vp
                    wp   = wp
                    n    = transformNormal v.n
                    ldir = V3f.Zero - vp.XYZ |> Vec.normalize
            } 
        } 