namespace OpcSelectionViewer.Picking

open Aardvark.UI
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Geometry
open OpcSelectionViewer
open OpcSelectionViewer.KdTrees
open Aardvark.SceneGraph.Opc

type PickingAction = 
  | HitSurface of Box3d*SceneHit    
  | RemoveLastPoint
  | ClearPoints


[<ModelType>]
type OpcData = {
  [<NonAdaptive>]
  patchHierarchy : PatchHierarchy
  kdTree         : HashMap<Box3d, Level0KdTree>

  localBB        : Box3d
  globalBB       : Box3d
}

[<ModelType>]
type PickingModel = {
  pickingInfos         : HashMap<Box3d, OpcData>
  intersectionPoints   : IndexList<V3d>  
}  

module PickingModel =

  let initial = 
    {
      pickingInfos       = HashMap.empty
      intersectionPoints = IndexList.empty
    }