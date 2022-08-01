namespace screenhotr.example.Model

open Aardvark.UI.Primitives
open Adaptify
open Aardvark.UI.Screenshotr

type Message =
    | CameraMessage       of FreeFlyController.Message
    | KeyDown             of key : Aardvark.Application.Keys
    | ScreenshotrMessage  of ScreenshotrMessage // Step 1: add a ScreenshotrMessage to your Message

[<ModelType>]
type Model =
    {
        cameraState : CameraControllerState
        screenshotr : ScreenshotrModel // Step 2: add a ScreenshotrModel to your Model
    }