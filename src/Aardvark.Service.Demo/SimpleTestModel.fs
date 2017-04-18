namespace SimpleTest

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Model = { 
    value : float 
    cameraModel : Demo.TestApp.CameraControllerState
}