namespace Niobe

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Geometry
open Aardvark.SceneGraph.Opc
open Aardvark.Geometry
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Application

type Message =
  | Camera           of FreeFlyController.Message
  | KeyUp            of key : Keys
  | KeyDown          of key : Keys  
  | UpdateDockConfig of DockConfig    

[<DomainType>]
type Model = 
  {
      cameraState          : CameraControllerState          
      threads              : ThreadPool<Message>
      dockConfig           : DockConfig
  }

