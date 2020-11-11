namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.UI.Anewmation
open Adaptify

[<ModelType>]
type Model =
    {
        cameraState : CameraControllerState
        background : C4b
        color : C4b
        rotation : float
        animator : Animator<Model>
    }

type Message =
    | Camera of FreeFlyController.Message
    | Animation of AnimatorMessage<Model>
    | CenterScene
    | ToggleAnimation
    | ToggleBackground