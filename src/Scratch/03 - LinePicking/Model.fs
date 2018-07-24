namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Action =
  | FreeFlyAction of CameraController.Message
  | PickPolygon   of SceneHit
  | KeyDown       of key : Aardvark.Application.Keys
  | KeyUp         of key : Aardvark.Application.Keys      

[<DomainType>]
type Model = 
    {
        camera    : CameraControllerState
        cylinders : Cylinder3d[]
        hitPoint  : option<V3d>
        isShift   : bool
    }