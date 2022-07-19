namespace screenhotr.example.Model


open Aardvark.Base
open Aardvark.UI.Primitives
open Adaptify
open Aardvark.UI.ScreenshotrService

type Message =
    | CameraMessage  of FreeFlyController.Message
    | KeyDown        of key : Aardvark.Application.Keys
    | ScreenshoterMessage of ScreenshotrAction

[<ModelType>]
type Model =
    {
        cameraState  : CameraControllerState

        imageSize    : V2i
        tags         : string[]
        screenshotrService : ScreenshotrService
    }