namespace screenhotr.example.Model


open Aardvark.Base
open Aardvark.UI.Primitives
open Adaptify
open Aardvark.UI.Screenshotr

type Message =
    | CameraMessage       of FreeFlyController.Message
    | KeyDown             of key : Aardvark.Application.Keys
    | ScreenshoterMessage of ScreenshotrMessage

[<ModelType>]
type Model =
    {
        cameraState : CameraControllerState
                    
        imageSize   : V2i
        tags        : string[]
        screenshotr : ScreenshotrModel
    }