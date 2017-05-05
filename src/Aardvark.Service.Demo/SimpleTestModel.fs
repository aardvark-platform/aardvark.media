namespace SimpleTest

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI

[<DomainType>]
type Model = { 
    value : float 
    cameraModel : CameraControllerState
}