namespace CameraAnimations

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
  | Camera           of FreeFlyController.Message
  | KeyUp            of key : Aardvark.Application.Keys
  | KeyDown          of key : Aardvark.Application.Keys
  | UpdateDockConfig of DockConfig

[<DomainType>]
type Model = 
  {
      cameraState          : CameraControllerState          
      threads              : ThreadPool<Message>
      dockConfig           : DockConfig
  }