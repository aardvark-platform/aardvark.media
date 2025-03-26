namespace OpcSelectionViewer

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Geometry
open Aardvark.Data.Opc
open Aardvark.Geometry
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Application
open Adaptify
open OpcSelectionViewer.Picking

type Message =
  | Camera           of FreeFlyController.Message
  | KeyUp            of key : Keys
  | KeyDown          of key : Keys  
  | UpdateDockConfig of DockConfig    
  | PickingAction    of PickingAction

[<ModelType>]
type Model =
    {
        cameraState          : CameraControllerState                       
        fillMode             : FillMode                                
        [<NonAdaptive>]
        patchHierarchies     : list<PatchHierarchy>        
        
        boxes                : list<Box3d>
        opcInfos             : HashMap<Box3d, OpcData>
        threads              : ThreadPool<Message>
        dockConfig           : DockConfig
        picking              : PickingModel
        pickingActive        : bool
    }
  
   