namespace OpcSelectionViewer

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open FShade
open Aardvark.UI.``F# Sg``

open OpcSelectionViewer.Picking

module SceneObjectHandling = 
  open Aardvark.SceneGraph.Opc
  open Aardvark.UI
  open Aardvark.UI

  //open Aardvark.Physics.Sky
  let transparent = RenderPass.after "transparent" RenderPassOrder.BackToFront RenderPass.main 

  let font = Font("Consolas")
  let border = { left = 0.01; right = 0.01; top = 0.01; bottom = 0.01 }
  
  let pickable' (pick :aval<Pickable>) (sg: ISg) =
    Sg.PickableApplicator (pick, AVal.constant sg)

  let createSingleOpcSg (m : AdaptiveModel) (data : Box3d*AdaptiveOpcData) =
    let boundingBox, opcData = data
    
    let leaves = 
      opcData.patchHierarchy.tree
        |> QTree.getLeaves 
        |> Seq.toList 
        |> List.map(fun y -> (opcData.patchHierarchy.opcPaths.Opc_DirAbsPath, y))
    
    let sg = 
      let config = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }
    
      leaves 
        |> List.map(fun (dir,patch) -> (Patch.load (OpcPaths dir) ViewerModality.XYZ patch.info,dir, patch.info)) 
        |> List.map(fun ((a,_),c,d) -> (a,c,d))
        |> List.map (fun (g,dir,info) -> 
        
          let texPath = Patch.extractTexturePath (OpcPaths dir) info 0
          let tex = FileTexture(texPath,config) :> ITexture
                    
          Sg.ofIndexedGeometry g
              |> Sg.trafo (AVal.constant info.Local2Global)             
              |> Sg.diffuseTexture (AVal.constant tex)             
          )
        |> Sg.ofList   
    
    let pickable = 
      adaptive {
        let! bb = opcData.globalBB
        return { shape = PickShape.Box bb; trafo = Trafo3d.Identity }
      }       
    
    sg      
      |> pickable' pickable
      |> Sg.noEvents      
      |> Sg.withEvents [
          SceneEventKind.Down, (
            fun sceneHit -> 
              let intersect = m.pickingActive |> AVal.force
              if intersect then              
                Stop, Seq.ofList[(HitSurface (boundingBox,sceneHit)) |> PickingAction]
              else 
                Continue, Seq.ofList[]
          )      
      ]

  
