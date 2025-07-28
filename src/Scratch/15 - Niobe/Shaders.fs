namespace Niobe

module Shader =
  open Aardvark.Rendering.Effects
  open Aardvark.Base
  open Aardvark.Rendering
  open FShade

  type SuperVertex = 
        {
            [<Position>] pos :  V4f
            [<SourceVertexIndex>] i : int
        }

  type Frag =
    {
        [<Color>] color : V4f
        [<Depth>] d : float32
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
    type MegaVertex = 
      {
        [<Position>]        pos      : V4f
        [<WorldPosition>]   wp       : V4f
        [<Normal>]          n        : V3f
        [<BiNormal>]        b        : V3f
        [<Tangent>]         t        : V3f
        [<Color>]           c        : V4f
        [<TexCoord>]        tc       : V2f
        [<Semantic("ViewPos")>] viewPos : V4f
        [<SourceVertexIndex>] i : int
      }

    let internal specialTrafo (v : Vertex) =
      vertex {
          let wp = uniform.ModelTrafo * v.pos
          return {
              pos = uniform.ViewProjTrafo * wp
              viewPos = uniform.ViewTrafo * wp
              wp = wp
              n = uniform.ModelTrafoInv.TransposedTransformDir v.n
              b = uniform.ModelTrafo.TransformDir v.b
              t = uniform.ModelTrafo.TransformDir v.t
              c = v.c
              tc = v.tc
              i = 0
          }
      }

    let internal colorDepth (v : MegaVertex) =
      fragment {
        let depth = 0.5f * v.viewPos.Z / v.viewPos.W + 0.5f
        return { color = v.c; d = depth }
      }
      
    let internal toCameraShift (p : Vertex) =
      vertex {
        let (offset : float32) = (uniform?depthOffset)
        
        let wp = p.wp
        let viewVec = ((wp.XYZ - uniform.CameraLocation).Normalized)
        let viewVec = V4f(viewVec.X, viewVec.Y, viewVec.Z, 0.0f)
        let wpShift = wp + viewVec * offset
        let posShift = uniform.ViewProjTrafo * wpShift

      return { p with pos = posShift; wp = wpShift }
      }

    let internal pointSprite (p : Point<Vertex>) =
      triangle {
        
        let s = uniform.PointSize / V2f uniform.ViewportSize
        let pos = p.Value.pos
        let pxyz = pos.XYZ / pos.W

        let p00 = V3f(pxyz + V3f( -s.X*0.33f, -s.Y,      0.0f ))
        let p01 = V3f(pxyz + V3f(  s.X*0.33f, -s.Y,      0.0f ))
        let p10 = V3f(pxyz + V3f( -s.X,      -s.Y*0.33f, 0.0f ))
        let p11 = V3f(pxyz + V3f(  s.X,      -s.Y*0.33f, 0.0f ))
        let p20 = V3f(pxyz + V3f( -s.X,       s.Y*0.33f, 0.0f ))
        let p21 = V3f(pxyz + V3f(  s.X,       s.Y*0.33f, 0.0f ))
        let p30 = V3f(pxyz + V3f( -s.X*0.33f,  s.Y,      0.0f ))
        let p31 = V3f(pxyz + V3f(  s.X*0.33f,  s.Y,      0.0f ))

        yield { p.Value with pos = V4f(p00 * pos.W, pos.W); tc = V2f (0.33f, 0.00f); }
        yield { p.Value with pos = V4f(p01 * pos.W, pos.W); tc = V2f (0.66f, 0.00f); }
        yield { p.Value with pos = V4f(p10 * pos.W, pos.W); tc = V2f (0.00f, 0.33f); }
        yield { p.Value with pos = V4f(p11 * pos.W, pos.W); tc = V2f (1.00f, 0.33f); }
        yield { p.Value with pos = V4f(p20 * pos.W, pos.W); tc = V2f (0.00f, 0.66f); }
        yield { p.Value with pos = V4f(p21 * pos.W, pos.W); tc = V2f (1.00f, 0.66f); }
        yield { p.Value with pos = V4f(p30 * pos.W, pos.W); tc = V2f (0.33f, 1.00f); }
        yield { p.Value with pos = V4f(p31 * pos.W, pos.W); tc = V2f (0.66f, 1.00f); }
      }

    let EffectCameraShift = 
        toEffect toCameraShift

    let EffectSprite = 
        toEffect pointSprite

  module LineRendering =
    type ThickLineVertex = {
        [<Position>]                pos     : V4f
        [<Color>]                   c       : V4f
        [<Semantic("LineCoord")>]   lc      : V2f
        [<Semantic("Width")>]       w       : float32
        [<Semantic("ViewPos")>]     vPos : V4f
    }
            
    let thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let offset : float32 = uniform?depthOffset
            let t = uniform.LineWidth                        
            //let pxyz = forward * depthOff            
                
            let sizeF = V3f(float32 uniform.ViewportSize.X, float32 uniform.ViewportSize.Y, 1.0f)
            
            let pp0 = line.P0.pos
            let pp1 = line.P1.pos
                                            
            //clipping
            let pp0 = if pp0.Z < 0.0f then (lerp pp1 pp0 (pp1.Z / (pp1.Z - pp0.Z))) else pp0
            let pp1 = if pp1.Z < 0.0f then (lerp pp0 pp1 (pp0.Z / (pp0.Z - pp1.Z))) else pp1
                    
            let p0 = pp0.XYZ / pp0.W
            let p1 = pp1.XYZ / pp1.W
                                    
            let fwp = (p1.XYZ - p0.XYZ) * sizeF
    
            let fw = V3f(fwp.XY * 2.0f, 0.0f) |> Vec.normalize
            let r = V3f(-fw.Y, fw.X, 0.0f) / sizeF
            let d = fw / sizeF
            let p00 = p0.XYZ - r * t - d * t
            let p10 = p0.XYZ + r * t - d * t
            let p11 = p1.XYZ + r * t + d * t
            let p01 = p1.XYZ - r * t + d * t
    
            let rel = t / (Vec.length fwp)
    
            yield { line.P0 with pos = V4f(p00, 1.0f); lc = V2f(-1.0f, -rel); w = rel }
            yield { line.P0 with pos = V4f(p10, 1.0f); lc = V2f( 1.0f, -rel); w = rel }
            yield { line.P1 with pos = V4f(p01, 1.0f); lc = V2f(-1.0f, 1.0f + rel); w = rel }
            yield { line.P1 with pos = V4f(p11, 1.0f); lc = V2f( 1.0f, 1.0f + rel); w = rel }
        }

    let thickLine2 (line : Line<ThickLineVertex>) =
        triangle {
            let offset : float32 = uniform?depthOffset
            let t = uniform.LineWidth                        
            //let pxyz = forward * depthOff            
                
            let sizeF = V3f(float32 uniform.ViewportSize.X, float32 uniform.ViewportSize.Y, 1.0f)
            
            let pp0 = line.P0.pos
            let pp1 = line.P1.pos
            let middle = (pp0 + pp1) / 2.0f
            let viewVec = ((middle.XYZ - uniform.CameraLocation).Normalized)
            let viewVec = V4f(viewVec.X, viewVec.Y, viewVec.Z, 1.0f)

            let pp0 = pp0 + viewVec * offset
            let pp1 = pp1 + viewVec * offset
                                            
            //clipping
            let pp0 = if pp0.Z < 0.0f then (lerp pp1 pp0 (pp1.Z / (pp1.Z - pp0.Z))) else pp0
            let pp1 = if pp1.Z < 0.0f then (lerp pp0 pp1 (pp0.Z / (pp0.Z - pp1.Z))) else pp1
                    
            let p0 = pp0.XYZ / pp0.W
            let p1 = pp1.XYZ / pp1.W
                       
            let fwp = (p1.XYZ - p0.XYZ) * sizeF
    
            let fw = V3f(fwp.XY * 2.0f, 0.0f) |> Vec.normalize
            let r = V3f(-fw.Y, fw.X, 0.0f) / sizeF
            let d = fw / sizeF

            let k = (d.Normalized |> Vec.cross r.Normalized).Normalized * offset      

            let p00 = p0.XYZ - r * t - d * t //+ k
            let p10 = p0.XYZ + r * t - d * t //+ k
            let p11 = p1.XYZ + r * t + d * t //+ k
            let p01 = p1.XYZ - r * t + d * t //+ k
    
            let rel = t / (Vec.length fwp)
    
            yield { line.P0 with pos = V4f(p00, 1.0f); lc = V2f(-1.0f, -rel); w = rel }
            yield { line.P0 with pos = V4f(p10, 1.0f); lc = V2f( 1.0f, -rel); w = rel }
            yield { line.P1 with pos = V4f(p01, 1.0f); lc = V2f(-1.0f, 1.0f + rel); w = rel }
            yield { line.P1 with pos = V4f(p11, 1.0f); lc = V2f( 1.0f, 1.0f + rel); w = rel }
        }
