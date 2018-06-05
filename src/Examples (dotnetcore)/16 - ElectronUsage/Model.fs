namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of CameraController.Message
    | CenterScene
    | LoadFiles of list<string>
    | SaveFile of string

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
    }