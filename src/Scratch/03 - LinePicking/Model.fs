namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

type Action =
  | FreeFlyAction of FreeFlyController.Message
  | PickPolygon   of SceneHit
  | KeyDown       of key : Aardvark.Application.Keys
  | KeyUp         of key : Aardvark.Application.Keys      

[<ModelType>]
type Model = 
    {
        camera    : CameraControllerState
        cylinders : Cylinder3d[]
        hitPoint  : option<V3d>
        isShift   : bool
    }