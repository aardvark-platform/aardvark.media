namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | ToggleBackground
    | UpdateInput of V2i
    | Result of string
    | Progress of string




[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
        background : C4b
        currentInput : MVar<V2i>
        result : string
        progress : string
    }