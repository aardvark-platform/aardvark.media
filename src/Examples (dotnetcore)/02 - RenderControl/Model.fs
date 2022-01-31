namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | ToggleBackground
    | Rendered
     
[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
        background : C4b
        time : float
    }