namespace SimpleTest

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives

[<ModelType>]
type Model = { 
    sphereFirst : bool
    value : float 
    cameraModel : CameraControllerState
}