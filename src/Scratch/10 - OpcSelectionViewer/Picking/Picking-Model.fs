namespace OpcSelectionViewer.Picking

open Aardvark.UI
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Geometry
open OpcSelectionViewer
open OpcSelectionViewer.KdTrees
open Aardvark.SceneGraph.Opc

type PickingAction = 
  | HitSurface of Box3d*SceneHit    
  | RemoveLastPoint
  | ClearPoints


[<DomainType>]
type OpcData = {
  [<NonIncremental>]
  patchHierarchy : PatchHierarchy
  kdTree         : hmap<Box3d, Level0KdTree>

  localBB        : Box3d
  globalBB       : Box3d
}

[<DomainType>]
type PickingModel = {
  pickingInfos         : hmap<Box3d, OpcData>
  intersectionPoints   : plist<V3d>  
}  

module PickingModel =

  let initial = 
    {
      pickingInfos       = HMap.empty
      intersectionPoints = PList.empty
    }