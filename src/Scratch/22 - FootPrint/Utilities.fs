namespace Utils

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.SceneGraph
open Aardvark.SceneGraph.SgPrimitives

module Footprint = 
    let getTrafo (trafo:Trafo3d) (model : MModel) =
        adaptive {
            let! cam = model.frustumCam2 
            let projMatrix = (cam |> Frustum.projTrafo).Forward
            let! view = model.camera2.view
            let instViewMatrix = view.ViewTrafo.Forward
            return (projMatrix * instViewMatrix ) //* trafo.Forward
        } 
    let getTexture =
        let res = V2i(1024, 1024)
       
        let pi = PixImage<byte>(Col.Format.RGBA, res)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) -> C4b.White) |> ignore
        PixTexture2d(PixImageMipMap [| (pi.ToPixImage(Col.Format.RGBA)) |], true) :> ITexture

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
            
            let instrumentMatrix    : M44d   = uniform?instrumentMVP
            
            return { v with tc0 = instrumentMatrix * v.wp; sv = 0 } //v.pos
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
                let t = v.tc0.XY / v.tc0.W
                //if t.X > -1.0 && t.X < 1.0 && t.Y > -1.0 && t.Y < 1.0 then
                //    return V4d(1.0, 0.0, 0.0, 1.0)
                let col = 
                    if t.X > -1.0 && t.X < 1.0 && t.Y > -1.0 && t.Y < 1.0 then
                        V4d(1.0, 0.0, 0.0, 1.0)
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
        |> AList.toMod 
        |> Mod.map (fun l ->
            let list = PList.toList l
            let head = list |> List.tryHead
                    
            match head with
                | Some h -> list
                            |> List.pairwise
                            |> List.map (fun (a,b) -> new Line3d(a,b))
                            |> List.toArray
                | None -> [||])
        |> Sg.lines (Mod.constant color)
        |> Sg.effect [
        toEffect DefaultSurfaces.stableTrafo
        toEffect DefaultSurfaces.vertexColor
        toEffect DefaultSurfaces.thickLine
        ]
        |> Sg.uniform "LineWidth" (Mod.constant 5.0)

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

  let scene (model : MModel) =

    let geom1 =
        [
            Sg.box (Mod.constant C4b.Magenta) (Mod.constant Box3d.Unit) 
            |> Sg.trafo (Mod.constant (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))))
            |> Sg.uniform "footprintVisible" (Mod.constant true)
            |> Sg.uniform "instrumentMVP" (Footprint.getTrafo (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))) model)
            //|> Sg.texture (Sym.ofString "FootPrintTexture") (Mod.constant Footprint.getTexture)

            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit) 
            |> Sg.trafo (Mod.constant (Trafo3d.Translation(V3d(1.0, 0.0, 2.0))))
            |> Sg.uniform "footprintVisible" (Mod.constant true)
            |> Sg.uniform "instrumentMVP" (Footprint.getTrafo (Trafo3d.Translation(V3d(1.0, 0.0, 2.0))) model)
            //|> Sg.texture (Sym.ofString "FootPrintTexture") (Mod.constant Footprint.getTexture)

            Drawing.drawPlane C4b.Green (Box2d(V2d(10.0), V2d(1.0)))
            |> Sg.trafo (Mod.constant (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))))
            |> Sg.uniform "footprintVisible" (Mod.constant true)
            |> Sg.uniform "instrumentMVP" (Footprint.getTrafo (Trafo3d.Translation(V3d(0.0, 0.0, 0.0))) model)

        ] |> Sg.ofList
    
    
    let regular sg = 
        sg
         |> Sg.cullMode (Mod.constant CullMode.Clockwise)
         |> Sg.depthTest (Mod.constant DepthTestMode.Less)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
                do! Shader.footprintV
                do! Shader.footPrintF
            }

    regular ([geom1] |> Sg.ofList) 
    

  let fullScene (model : MModel) =
    let scenesgs = scene model
    let camtrafo = model.position.value |> Mod.map Trafo3d.Translation
    let camPoint = Drawing.mkISg (Mod.constant C4b.Yellow) (Mod.constant 0.3) camtrafo

    let lookAt = 
        alist {
            let! p = model.position.value
            let! view = model.camera2.view
            let look = view.Forward * 5.0
            yield p
            yield p + look
        }

    let lookAtSg = Drawing.drawColoredEdges C4b.Red lookAt

    let up = 
        alist {
            let! p = model.position.value
            let! view = model.camera2.view
            let up = view.Up * 5.0
            yield p
            yield p + up
        }
        
    let upSg = Drawing.drawColoredEdges C4b.Blue up
    let tSg = 
        scenesgs
            |> Sg.andAlso (
                    Sg.frustum (C4b.Yellow |> Mod.constant) model.camera2.view model.frustumCam2
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
                  )
    Sg.ofSeq [tSg; upSg] |> Sg.noEvents //scenesgs; camPoint; lookAtSg; upSg; frustum

  let camScene (model : MModel) =
    let scenesgs = scene model
    Sg.ofSeq [scenesgs] |> Sg.noEvents 



