namespace OpcSelectionViewer

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Geometry
open Aardvark.SceneGraph.Opc
open Aardvark.Geometry
open Aardvark.UI
open Aardvark.UI.Primitives
open KdTrees
open Aardvark.Application

type Message =
  | Camera       of FreeFlyController.Message
  | KeyUp        of key : Keys
  | KeyDown      of key : Keys
  | HitSurface   of Box3d*SceneHit    

[<DomainType>]
type OpcData = {
  [<NonIncremental>]
  patchHierarchy : PatchHierarchy
  kdTree         : hmap<Box3d, Level0KdTree>

  localBB        : Box3d
  globalBB       : Box3d
}

[<DomainType>]
type Model =
    {
        cameraState          : CameraControllerState                       
        fillMode             : FillMode                                
        opcInfos             : hmap<Box3d, OpcData>
        [<NonIncremental>]
        patchHierarchies     : list<PatchHierarchy>        
        [<NonIncremental>]
        kdTrees2             : hmap<Box3d, Level0KdTree>                
        boxes                : list<Box3d>        
        intersectionPoints   : V3f[]                                
        threads              : ThreadPool<Message>
        intersection         : bool
    }
  
   