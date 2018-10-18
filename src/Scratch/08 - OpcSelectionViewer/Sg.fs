namespace OpcSelectionViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Text

open FShade

module Sg = 
  open Aardvark.SceneGraph.Opc

  //open Aardvark.Physics.Sky
  let transparent = RenderPass.after "transparent" RenderPassOrder.BackToFront RenderPass.main 

  let font = Font("Consolas")
  let border = { left = 0.01; right = 0.01; top = 0.01; bottom = 0.01 }
  
  let opcSg (m:MModel) preTransform = 
    
    Opc.createFlatISg m.patchHierarchies        
        |> Sg.trafo preTransform
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
        }
        

  let boxes (m:MModel) =              
    m.boxes 
     |> Mod.map(fun boxes -> 
       boxes |> List.map(fun b -> Sg.wireBox' C4b.Red b) |> ASet.ofList |> Sg.set) 
     |> Sg.dynamic
     //|> Sg.pass transparent
     //|> Sg.blendMode (Mod.constant BlendMode.Blend)    
     |> Sg.shader {
         do! DefaultSurfaces.trafo
         do! DefaultSurfaces.constantColor C4f.Red
     }
     |> Sg.trafo m.finalTransform

  let pickSpheres (m:MDipAndStrike) =    
    m.points
      |> AList.map(fun x -> 
        Sg.sphere' 4 C4b.Magenta 0.015
          |> Sg.trafo (x |> Trafo3d.Translation |> Mod.constant))
      |> ASet.ofAList
      |> Sg.set
      |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
      }            
        


  let discISg color size height trafo =
    Sg.cylinder 30 color size height                  
      |> Sg.pass transparent
      |> Sg.blendMode (Mod.constant BlendMode.Blend)    
      |> Sg.shader {          
          do! DefaultSurfaces.trafo
          do! DefaultSurfaces.vertexColor
          //do! Aardvark.Opc.Shader.stableLight
      }
      |> Sg.trafo(trafo)

  let drawDns(m:MDipAndStrike) =
    aset {
      let! dns = m.results
      match dns with
      | Some result -> 
        let! angle = result.dipAngle 
        let max = 45.0
        let center = m.points |> AList.toMod |> Mod.map (fun list -> list.[PList.count list / 2])
        let hue = (1.0 - (angle |> clamp 0.0 max) / max) * (2.0 / 3.0)
        let color = HSVf(hue, 1.0, 1.0).ToC3f().ToC3b()
        let color = C4b(color.R, color.G, color.B, 88uy)

        let size = m.points |> AList.toMod |> Mod.map (fun list -> Box3d(list.AsArray).Size.Length / 2.5)

        let posTrafo = (center |> Mod.map Trafo3d.Translation) 
        let discTrafo = Mod.map2(fun (pln:Plane3d) pos -> (Trafo3d.RotateInto(V3d.ZAxis, pln.Normal) * pos)) result.plane posTrafo
        
        yield discISg (color |> Mod.constant) size (0.01 |> Mod.constant) discTrafo
      | None -> ()

    } |> Sg.set

  let drawWorkingDns (m:MModel) = 
    m.workingDns
      |> Mod.map(function 
        | Some x -> 
          [
            pickSpheres x |> Sg.depthTest (DepthTestMode.None |> Mod.constant)
            drawDns x
          ] |> Sg.ofList
        | None -> Sg.empty)
      |> Sg.dynamic
      |> Sg.trafo m.finalTransform
      

  let coord (m:MModel) =
    let x =  Sg.cylinder' 32 C4b.Red 0.01 1.5
    let y =  Sg.cylinder' 32 C4b.Green 0.01 1.5
    let z =  Sg.cylinder' 32 C4b.Blue 0.01 1.5
    
    //let trafo = 
    //    m.picked 
    //        |> AMap.toMod
    //        |> Mod.map (fun map -> map |> HMap.toArray |> Array.map (fun (_,pt) -> Trafo3d.Translation(pt)))
    
    Sg.ofList [
        z
        x |> Sg.transform (Trafo3d.FromBasis(V3d.OIO, V3d.OOI, V3d.IOO, V3d.Zero))
        y |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero))
    ]
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.simpleLighting
    }
    |> Sg.trafo (Trafo3d.Identity |> Mod.constant)
   // |> Sg.instanced trafo
