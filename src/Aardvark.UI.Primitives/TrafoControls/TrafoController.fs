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

    let toRotTrafo' (pose:IMod<Pose>) : IMod<Trafo3d> = 
        pose |> Mod.map(fun x -> Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse))

    let toTrafo x = 
        let rot = x |> toRotTrafo
        Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position

    let toTrafo' (pose : IMod<Pose>) : IMod<Trafo3d> =
        pose |> Mod.map(fun x ->
            let rot = x |> toRotTrafo
            Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position)        

    let trafoWoScale x = 
        (x |> toRotTrafo) * Trafo3d.Translation x.position

    let toTranslateTrafo x =
        Trafo3d.Translation x.position

    let transform (p : Pose) (t : Trafo3d) = 
        let newRot = Rot3d.FromFrame(t.Forward.C0.XYZ,t.Forward.C1.XYZ,t.Forward.C2.XYZ)
        { position = t.Forward.TransformPosProj p.position; rotation = newRot; scale = t.Forward.TransformPos V3d.III } // wrong

    let combine (left:Pose) (right:Pose) =
      { position = left.position + right.position; rotation = left.rotation * right.rotation; scale = left.scale + right.scale }


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
            scale        = 1.0
            preTransform = Pose.identity
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

    let pickingTrafo (m:MTransformation) : IMod<Trafo3d> =
        adaptive {
            let! mode = m.mode
            let! p = m.pose                
           
            match mode with
                | TrafoMode.Local -> 
                    return p.Trafo
                | TrafoMode.Global ->                     
                    return Trafo3d.Translation(p.position)
                | _ -> 
                    return failwith ""
        }

    let getTranslation (t : IMod<Trafo3d>) =
        t |> Mod.map(fun x -> x.Forward.C3.XYZ)

module Sg =
    open Aardvark.Base
    open Aardvark.Base.Incremental

    let computeInvariantScale (view : CameraView) (near : float) (p : V3d) (size : float) (hfov : float) =                    
        let hfov_rad = Conversion.RadiansFromDegrees(hfov)               
        let wz = Fun.Tan(hfov_rad / 2.0) * near * size
        let dist = V3d.Distance(p, view.Location)

        ( wz / near ) * dist

    let computeInvariantScale' (view : IMod<CameraView>) (near : IMod<float>) (p:IMod<V3d>) (size:IMod<float>) (hfov:IMod<float>) =
        adaptive {
            let! p = p
            let! v = view
            let! near = near
            let! size = size
            let! hfov = hfov       
               
            return computeInvariantScale v near p size hfov

            //let wz = Fun.Tan(hfov_rad / 2.0) * near * size
            //let dist = V3d.Distance(p, v.Location)

            //return ( wz / near ) * dist
        }

        


module Shader =
        
    open Aardvark.Base
    open Aardvark.Base.IO
    open Aardvark.Base.Rendering
    open Aardvark.Base.Rendering.Effects
    
    open FShade

    type Vertex =
        {
            [<Position>]                pos     : V4d            
            [<WorldPosition>]           wp      : V4d
            [<TexCoord>]                tc      : V2d
            [<Color>]                   c       : V4d
            [<Normal>]                  n       : V3d
            [<Semantic("Scalar")>]      scalar  : float
            [<Semantic("LightDir")>]    ldir    : V3d
        }

    let hoverColor (v : Vertex) =
        vertex {
            let c : V4d = uniform?HoverColor
            return { v with c = c }
        }

    [<ReflectedDefinition>]
    let transformNormal (n : V3d) =
        uniform.ModelViewTrafoInv.Transposed * V4d(n, 0.0)
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
                    ldir = V3d.Zero - vp.XYZ |> Vec.normalize
            } 
        } 