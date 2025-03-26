namespace Niobe

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Geometry
open Aardvark.Geometry
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Application
open Niobe.Sketching
open Adaptify

type Message =
  | Camera           of FreeFlyController.Message  
  | KeyUp            of key : Keys
  | KeyDown          of key : Keys  
  | UpdateDockConfig of DockConfig    
  | HitSurface       of V3d
  | SketchingMessage of SketchingAction
  | ToggleShadowVolumeVis
  | ToggleLineVis

[<ModelType>]
type Model = 
  {
      cameraState          : CameraControllerState          
      threads              : ThreadPool<Message>
      dockConfig           : DockConfig
      picking              : bool
      sketching            : SketchingModel
      shadowVolumeVis      : bool
      showLines            : bool
  }

