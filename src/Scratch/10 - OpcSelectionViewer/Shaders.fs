namespace OpcSelectionViewer

module Shader =
  open Aardvark.Base.Rendering.Effects
  open Aardvark.Base
  open Aardvark.Base.Rendering
  open FShade

  module PointSprite = 
    let internal pointSprite (p : Point<Vertex>) =
      triangle {
        let s = uniform.PointSize / V2d uniform.ViewportSize
        let pos = p.Value.pos
        let pxyz = pos.XYZ / pos.W

        let p00 = V3d(pxyz + V3d( -s.X*0.33, -s.Y, 0.0 ))
        let p01 = V3d(pxyz + V3d(  s.X*0.33, -s.Y, 0.0 ))
        let p10 = V3d(pxyz + V3d( -s.X,      -s.Y*0.33, 0.0 ))
        let p11 = V3d(pxyz + V3d(  s.X,      -s.Y*0.33, 0.0 ))
        let p20 = V3d(pxyz + V3d( -s.X,       s.Y*0.33, 0.0 ))
        let p21 = V3d(pxyz + V3d(  s.X,       s.Y*0.33, 0.0 ))
        let p30 = V3d(pxyz + V3d( -s.X*0.33,  s.Y, 0.0 ))
        let p31 = V3d(pxyz + V3d(  s.X*0.33,  s.Y, 0.0 ))

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

  module PointSpriteQuad =       
    let internal pointSpriteQuad (p : Point<Vertex>) =
      triangle {
        let s = (uniform.PointSize / V2d uniform.ViewportSize)
        let pos = p.Value.pos
        let pxyz = pos.XYZ / pos.W

        let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
        let p01 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
        let p10 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))
        let p11 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))

        yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d (0.00, 0.00); }
        yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d (1.00, 0.00); }
        yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d (0.00, 1.00); }          
        yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d (1.00, 1.00); }
      }
    
    let Effect = 
      toEffect pointSpriteQuad