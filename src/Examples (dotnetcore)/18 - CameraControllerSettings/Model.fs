namespace RenderControl.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
    }