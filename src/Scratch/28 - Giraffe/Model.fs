namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | OrbitMessage of OrbitMessage
    | CenterScene
    | ToggleBackground
    | Nop

[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
        orbitState : OrbitState
        background : C4b
    }