namespace screenhotr.example.Model

open Aardvark.UI.Primitives
open Adaptify
open Aardvark.UI.Screenshotr

type Message =
    | CameraMessage       of FreeFlyController.Message
    | KeyDown             of key : Aardvark.Application.Keys
    | ScreenshoterMessage of ScreenshotrMessage // step 1: add a ScreenshotrMessage to your Message

[<ModelType>]
type Model =
    {
        cameraState : CameraControllerState
        screenshotr : ScreenshotrModel // step 2: add a ScreenshotrModel to your Model
    }