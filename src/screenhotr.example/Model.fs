namespace screenhotr.example.Model

open Aardvark.UI.Primitives
open Adaptify

[<ModelType>]
type Model =
    {
        cameraState     : CameraControllerState
    }
