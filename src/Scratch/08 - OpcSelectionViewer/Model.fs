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
    //| KeyDown    of key : Aardvark.Application.Keys
    //| KeyUp      of key : Aardvark.Application.Keys  

[<DomainType>]
type DipAndStrikeResults = {
    plane           : Plane3d
    dipAngle        : float
    dipDirection    : V3d
    strikeDirection : V3d
    dipAzimuth      : float
    strikeAzimuth   : float
    centerOfMass    : V3d    
}

[<DomainType>]
type DipAndStrike = {
  results : option<DipAndStrikeResults>
  points  : plist<V3d>
}

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
        distance             : Option<string>
        line                 : Option<Line3d>
        fillMode             : FillMode
        //renderLinePersistent : bool
        renderLine           : bool
        showRay              : Option<int>
        teleportTrafo        : Option<Trafo3d>                
        teleportBeacon       : Option<V3d>        
        picked               : hmap<int, V3d>  
        opcInfos             : hmap<Box3d, OpcData>
        [<NonIncremental>]
        patchHierarchies     : list<PatchHierarchy>
        [<NonIncremental>]
        kdTrees              : list<KdTree<Triangle3d> * Trafo3d>
        [<NonIncremental>]
        kdTrees2             : hmap<Box3d, Level0KdTree>
        
        
        boxes                : list<Box3d>
        lines                : list<Line3d>

        workingDns           : option<DipAndStrike>

        initialTransform     : Trafo3d        
        finalTransform       : Trafo3d

        threads              : ThreadPool<Message>

        intersection         : bool
    }
  
   