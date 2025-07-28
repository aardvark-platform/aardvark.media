namespace OpcSelectionViewer

module Shader =
  open Aardvark.Rendering.Effects
  open Aardvark.Base
  open Aardvark.Rendering
  open FShade

  module PointSprite = 
    let internal pointSprite (p : Point<Vertex>) =
      triangle {
        let s = uniform.PointSize / V2f uniform.ViewportSize
        let pos = p.Value.pos
        let pxyz = pos.XYZ / pos.W

        let p00 = V3f(pxyz + V3f( -s.X*0.33f, -s.Y, 0.0f ))
        let p01 = V3f(pxyz + V3f(  s.X*0.33f, -s.Y, 0.0f ))
        let p10 = V3f(pxyz + V3f( -s.X,      -s.Y*0.33f, 0.0f ))
        let p11 = V3f(pxyz + V3f(  s.X,      -s.Y*0.33f, 0.0f ))
        let p20 = V3f(pxyz + V3f( -s.X,       s.Y*0.33f, 0.0f ))
        let p21 = V3f(pxyz + V3f(  s.X,       s.Y*0.33f, 0.0f ))
        let p30 = V3f(pxyz + V3f( -s.X*0.33f,  s.Y, 0.0f ))
        let p31 = V3f(pxyz + V3f(  s.X*0.33f,  s.Y, 0.0f ))

        yield { p.Value with pos = V4f(p00 * pos.W, pos.W); tc = V2f (0.33f, 0.00f); }
        yield { p.Value with pos = V4f(p01 * pos.W, pos.W); tc = V2f (0.66f, 0.00f); }
        yield { p.Value with pos = V4f(p10 * pos.W, pos.W); tc = V2f (0.00f, 0.33f); }
        yield { p.Value with pos = V4f(p11 * pos.W, pos.W); tc = V2f (1.00f, 0.33f); }
        yield { p.Value with pos = V4f(p20 * pos.W, pos.W); tc = V2f (0.00f, 0.66f); }
        yield { p.Value with pos = V4f(p21 * pos.W, pos.W); tc = V2f (1.00f, 0.66f); }
        yield { p.Value with pos = V4f(p30 * pos.W, pos.W); tc = V2f (0.33f, 1.00f); }
        yield { p.Value with pos = V4f(p31 * pos.W, pos.W); tc = V2f (0.66f, 1.00f); }
      }

    let Effect = 
        toEffect pointSprite

  module PointSpriteQuad =       
    let internal pointSpriteQuad (p : Point<Vertex>) =
      triangle {
        let s = (uniform.PointSize / V2f uniform.ViewportSize)
        let pos = p.Value.pos
        let pxyz = pos.XYZ / pos.W

        let p00 = V3f(pxyz + V3f( -s.X, -s.Y, 0.0f ))
        let p01 = V3f(pxyz + V3f(  s.X, -s.Y, 0.0f ))
        let p10 = V3f(pxyz + V3f(  s.X,  s.Y, 0.0f ))
        let p11 = V3f(pxyz + V3f( -s.X,  s.Y, 0.0f ))

        yield { p.Value with pos = V4f(p00 * pos.W, pos.W); tc = V2f (0.00f, 0.00f); }
        yield { p.Value with pos = V4f(p01 * pos.W, pos.W); tc = V2f (1.00f, 0.00f); }
        yield { p.Value with pos = V4f(p11 * pos.W, pos.W); tc = V2f (0.00f, 1.00f); }
        yield { p.Value with pos = V4f(p10 * pos.W, pos.W); tc = V2f (1.00f, 1.00f); }
      }
    
    let Effect = 
      toEffect pointSpriteQuad