namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of CameraController.Message
    | CenterScene

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
    }