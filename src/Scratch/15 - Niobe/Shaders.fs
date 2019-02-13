namespace Niobe

module Shader =
  open Aardvark.Base.Rendering.Effects
  open Aardvark.Base
  open Aardvark.Base.Rendering
  open FShade

  type SuperVertex = 
        {
            [<Position>] pos :  V4d
            [<SourceVertexIndex>] i : int
        }

  let lines (t : Triangle<SuperVertex>) =
        line {
            yield t.P0
            yield t.P1
            restartStrip()
            
            yield t.P1
            yield t.P2
            restartStrip()

            yield t.P2
            yield t.P0
            restartStrip()
        }

  module PointSprite = 
    let internal pointSprite (p : Point<Vertex>) =
      triangle {
        let s = uniform.PointSize / V2d uniform.ViewportSize

        let pos = p.Value.pos

     //   let (view : M44d) = uniform?ViewTrafo
     //   let forward = view.C2.XYZ

        let (offset : float) = (uniform?depthOffset) * pos.W
        let pxyz = (pos.XYZ) / pos.W

        let p00 = V3d(pxyz + V3d( -s.X*0.33, -s.Y,      offset ))
        let p01 = V3d(pxyz + V3d(  s.X*0.33, -s.Y,      offset ))
        let p10 = V3d(pxyz + V3d( -s.X,      -s.Y*0.33, offset ))
        let p11 = V3d(pxyz + V3d(  s.X,      -s.Y*0.33, offset ))
        let p20 = V3d(pxyz + V3d( -s.X,       s.Y*0.33, offset ))
        let p21 = V3d(pxyz + V3d(  s.X,       s.Y*0.33, offset ))
        let p30 = V3d(pxyz + V3d( -s.X*0.33,  s.Y,      offset ))
        let p31 = V3d(pxyz + V3d(  s.X*0.33,  s.Y,      offset ))

        yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d (0.33, 0.00); }
        yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d (0.66, 0.00); }
        yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d (0.00, 0.33); }
        yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d (1.00, 0.33); }
        yield { p.Value with pos = V4d(p20 * pos.W, pos.W); tc = V2d (0.00, 0.66); }
        yield { p.Value with pos = V4d(p21 * pos.W, pos.W); tc = V2d (1.00, 0.66); }
        yield { p.Value with pos = V4d(p30 * pos.W, pos.W); tc = V2d (0.33, 1.00); }
        yield { p.Value with pos = V4d(p31 * pos.W, pos.W); tc = V2d (0.66, 1.00); }
      }

    let Effect = 
        toEffect pointSprite

  module LineRendering =
    type ThickLineVertex = {
        [<Position>]                pos     : V4d
        [<Color>]                   c       : V4d
        [<Semantic("LineCoord")>]   lc      : V2d
        [<Semantic("Width")>]       w       : float
    }
    
    [<GLSLIntrinsic("mix({0}, {1}, {2})")>]
    let Lerp (a : V4d) (b : V4d) (s : float) : V4d = failwith ""
    
    let thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let depthOff : float = uniform?DepthOffset

            let (view : M44d) = uniform?ViewTrafo
            let forward = view.C2.XYZ                        
            let pxyz = forward * depthOff            
    
            

            let sizeF = V3d(float uniform.ViewportSize.X, float uniform.ViewportSize.Y, 1.0)
            
            let pp0 = line.P0.pos
            let pp1 = line.P1.pos
    
            //let wp = uniform.ModelTrafo * ((pp0 + pp1) / 2.0)
            //let dist = uniform.CameraLocation - wp.XYZ
            //let dir = (uniform.ModelTrafoInv * dist.XYZO).Normalized
    
            //let pp0 = pp0 + dir * depthOff
            //let pp1 = pp1 + dir * depthOff
            
    
            let pp0 = if pp0.Z < 0.0 then (Lerp pp1 pp0 (pp1.Z / (pp1.Z - pp0.Z))) else pp0
            let pp1 = if pp1.Z < 0.0 then (Lerp pp0 pp1 (pp0.Z / (pp0.Z - pp1.Z))) else pp1            
    
    
            let p0 = pp0.XYZ / pp0.W
            let p1 = pp1.XYZ / pp1.W
    
            let p0 = V3d(p0.X, p0.Y, p0.Z + depthOff)
            let p1 = V3d(p1.X, p1.Y, p1.Z + depthOff)

            let fwp = (p1.XYZ - p0.XYZ) * sizeF
    
            let fw = V3d(fwp.XY * 2.0, 0.0) |> Vec.normalize
            let r = V3d(-fw.Y, fw.X, 0.0) / sizeF
            let d = fw / sizeF
            let p00 = p0 - r * t - d * t
            let p10 = p0 + r * t - d * t
            let p11 = p1 + r * t + d * t
            let p01 = p1 - r * t + d * t
    
            let rel = t / (Vec.length fwp)
    
            yield { line.P0 with pos = V4d(p00, 1.0); lc = V2d(-1.0, -rel); w = rel }
            yield { line.P0 with pos = V4d(p10, 1.0); lc = V2d( 1.0, -rel); w = rel }
            yield { line.P1 with pos = V4d(p01, 1.0); lc = V2d(-1.0, 1.0 + rel); w = rel }
            yield { line.P1 with pos = V4d(p11, 1.0); lc = V2d( 1.0, 1.0 + rel); w = rel }
        }