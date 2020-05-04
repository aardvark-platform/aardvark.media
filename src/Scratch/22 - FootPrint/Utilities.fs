namespace Utils

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Model

open Aardvark.SceneGraph
open Aardvark.SceneGraph.SgPrimitives

module Footprint = 
    let getTrafo (view:aval<CameraView>) (frustum : aval<Frustum>) =
        adaptive {
            let! fr = frustum 
            let projMatrix = (fr |> Frustum.projTrafo).Forward
            let! view = view
            let instViewMatrix = view.ViewTrafo.Forward
            return (projMatrix * instViewMatrix ) //* trafo.Forward
        } 

    let getTexture =
        let res = V2i(1024, 1024)
       
        let pi = PixImage<byte>(Col.Format.RGBA, res)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) -> C4b.White) |> ignore
        PixTexture2d(PixImageMipMap [| (pi.ToPixImage(Col.Format.RGBA)) |], true) :> ITexture

    let checkerboard = 
        let res = V2i(1024, 1024)
        let texture = PixImage<byte>(Col.Format.RGBA, res)
        texture.GetMatrix<C4b>().SetByCoord(fun (c : V2l) -> let x' = c.X /(int64)32
                                                             let y' = c.Y /(int64)32
                                                             let i = x' + y'
                                                             (if (i % (int64)2) = (int64)0 then C4b.White else C4b.Black)
                                                             ) |> ignore   
        PixTexture2d(PixImageMipMap [| (texture.ToPixImage(Col.Format.RGBA)) |], true) :> ITexture

module Shader =
    open FShade
   
    type FootPrintVertex =
        {
            [<Position>]                pos     : V4d            
            [<WorldPosition>]           wp      : V4d
            [<TexCoord>]                tc      : V2d
            [<Color>]                   c       : V4d
            [<Normal>]                  n       : V3d
            [<SourceVertexIndex>]       sv      : int
            [<Semantic("Scalar")>]      scalar  : float
            [<Semantic("LightDir")>]    ldir    : V3d
            [<Semantic("Tex0")>]        tc0     : V4d
            [<Semantic("Tex1")>]        tc1     : V4d

        }

    let private footprintmap =
        sampler2d {
            texture uniform?FootPrintTexture
            filter Filter.MinMagMipLinear
            borderColor C4f.Black
            addressU WrapMode.Border
            addressV WrapMode.Border
            addressW WrapMode.Border
        }  
  
    let footprintV (v : FootPrintVertex) =
        vertex {
            //let vp = uniform.ModelViewTrafo * v.pos
            //let p = uniform.ProjTrafo * vp
            
            let footprintProjM  : M44d   = uniform?footprintProj
            let textureProjM    : M44d   = uniform?textureProj
            
            return { v with tc0 = footprintProjM * v.wp; 
                            tc1 = textureProjM   * v.wp; 
                            sv = 0 } //v.pos
        }

    let footprintV2 (v : FootPrintVertex) =
        vertex {
            let instrumentMatrix    : M44d   = uniform?instrumentMVP          
            let vp = uniform.ModelViewTrafo * v.pos
            let wp = uniform.ModelTrafo * v.pos

            return { 
                v with
                    pos  = uniform.ProjTrafo * vp
                    wp   = wp
                    //n    = transformNormal v.n
                    ldir = V3d.Zero - vp.XYZ |> Vec.normalize
                    tc0  = instrumentMatrix * v.pos                  

            } 
        } 

    let footPrintF (v : FootPrintVertex) =
        fragment {           
            if uniform?footprintVisible then
                let fpt = v.tc0.XY / v.tc0.W
                let tt  = v.tc1.XY / v.tc1.W
                let col = 
                    if (fpt.X > -1.0 && fpt.X < 1.0 && fpt.Y > -1.0 && fpt.Y < 1.0 && tt.X > -1.0 && tt.X < 1.0 && tt.Y > -1.0 && tt.Y < 1.0 ) then
                        let tt1 = (tt + 1.0)/2.0
                        //let tt2 = (tt * 2.0) - 1.0
                        V4d(1.0, 0.0, 0.0, 1.0) * (footprintmap.Sample(tt1))
                    elif fpt.X > -1.0 && fpt.X < 1.0 && fpt.Y > -1.0 && fpt.Y < 1.0 then
                        V4d(1.0, 0.0, 0.0, 1.0)
                    elif tt.X > -1.0 && tt.X < 1.0 && tt.Y > -1.0 && tt.Y < 1.0 then
                        let tt1 = (tt + 1.0)/2.0
                        footprintmap.Sample(tt1)
                    else
                        v.c 
                        
               
                if (v.tc0.Z <= 0.0) || (v.tc1.Z <= 0.0) then
                    return v.c
                else
                    return col 
                        
            else
            return v.c
        }

    let textureF (v : FootPrintVertex) =
        fragment {           
            if uniform?footprintVisible then
                let t = v.tc0.XY / v.tc0.W
                let col = 
                    if t.X > -1.0 && t.X < 1.0 && t.Y > -1.0 && t.Y < 1.0 then
                        let t1 = (t + 1.0)/2.0
                        //let t2 = (t * 2.0) - 1.0
                        footprintmap.Sample(t1)
                    else
                        v.c 
                if (v.tc0.Z <= 0.0) then
                    return v.c
                else
                    return col 
            else
            return v.c
        }

    let footPrintF2 (v : FootPrintVertex) =
      fragment {           
          if uniform?footprintVisible then
            let proTex0 = v.tc0.XY / v.tc0.W
            let c = footprintmap.Sample(proTex0)
            let col = 
                if (c.W <= 0.50) then
                    V4d(1.0, 1.0, 1.0, 1.0)
                elif (c.W <= 0.999) then
                    V4d(1.0, 0.0, 0.0, 1.0)
                else
                    v.c
            
            
            //if (v.tc0.Z <= 0.0) then
            //    return V4d(1.0, 1.0, 1.0, 1.0)
            //else
            return col 
          else
            return v.c
      }

module Drawing =

  let drawColoredEdges (color : C4b) (points : alist<V3d>) = 
    points
        |> AList.toAVal 
        |> AVal.map (fun l ->
            let list = IndexList.toList l
            let head = list |> List.tryHead
                    
            match head with
                | Some h -> list
                            |> List.pairwise
                            |> List.map (fun (a,b) -> new Line3d(a,b))
                            |> List.toArray
                | None -> [||])
        |> Sg.lines (AVal.constant color)
        |> Sg.effect [
        toEffect DefaultSurfaces.stableTrafo
        toEffect DefaultSurfaces.vertexColor
        toEffect DefaultSurfaces.thickLine
        ]
        |> Sg.uniform "LineWidth" (AVal.constant 5.0)

  let mkISg color size trafo =         
    Sg.sphere 5 color size 
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.noEvents
        |> Sg.trafo(trafo) 
  
  let drawPlane (color : C4b) (bounds : Box2d) = 
    let points = bounds.ComputeCorners() |> Array.map (fun x -> V3d(x.X, x.Y, 0.0)) |> Array.take 4
    
    IndexedGeometryPrimitives.quad (points.[0], color) (points.[1], color) (points.[3], color) (points.[2], color)  |> Sg.ofIndexedGeometry    

module Scene =
  
  let scene (model : AdaptiveModel) =

    let geom1 =
        [
            Sg.box (AVal.constant C4b.Magenta) (AVal.constant Box3d.Unit) 
            |> Sg.trafo (AVal.constant (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))))

            Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit) 
            |> Sg.trafo (AVal.constant (Trafo3d.Translation(V3d(1.0, 0.0, 2.0))))

            Drawing.drawPlane C4b.Green (Box2d(V2d(10.0), V2d(1.0)))
            |> Sg.trafo (AVal.constant (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))))

        ] |> Sg.ofList
    
    
    let regular sg = 
        sg
         |> Sg.cullMode (AVal.constant CullMode.Back)
         |> Sg.depthTest (AVal.constant DepthTestMode.Less)
         |> Sg.uniform "footprintVisible" (AVal.constant true)
         |> Sg.uniform "footprintProj" (Footprint.getTrafo model.footprintProj.cam.view model.footprintProj.frustum)
         |> Sg.uniform "textureProj" (Footprint.getTrafo model.textureProj.cam.view model.textureProj.frustum)
         |> Sg.texture (Sym.ofString "FootPrintTexture") (AVal.constant Footprint.checkerboard)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
                do! Shader.footprintV
                do! Shader.footPrintF
                //do! Shader.textureF
            }

    regular ([geom1] |> Sg.ofList) 
    

  let fullScene (model : AdaptiveModel) =
    let scenesgs = scene model
    let camtrafo = model.position.value |> AVal.map Trafo3d.Translation
    let camPoint = Drawing.mkISg (AVal.constant C4b.Yellow) (AVal.constant 0.3) camtrafo

    let lookAt = 
        alist {
            let! p = model.position.value
            let! view = model.camera2.view
            let look = view.Forward * 5.0
            yield p
            yield (p + look)
        }

    let lookAtSg = Drawing.drawColoredEdges C4b.Red lookAt

    let up = 
        alist {
            let! p = model.position.value
            let! view = model.camera2.view
            let up = view.Up * 5.0
            yield p
            yield (p + up)
        }
        
    let upSg = Drawing.drawColoredEdges C4b.Blue up

    let features = 
      alist {
            yield Sg.frustum (C4b.Yellow |> AVal.constant) model.footprintProj.cam.view model.footprintProj.frustum
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            yield Sg.frustum (C4b.Cyan |> AVal.constant) model.textureProj.cam.view model.textureProj.frustum
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
      } |> AList.toASet |> Sg.set

    let tSg = 
        scenesgs
            |> Sg.andAlso features
    Sg.ofSeq [tSg; upSg] |> Sg.noEvents //scenesgs; camPoint; lookAtSg; upSg; frustum

  let camScene (model : AdaptiveModel) =
    let scenesgs = scene model
    Sg.ofSeq [scenesgs] |> Sg.noEvents 



