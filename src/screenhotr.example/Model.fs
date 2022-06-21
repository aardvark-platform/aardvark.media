namespace screenhotr.example.Model

open Aardvark.Base
open Aardvark.UI.Primitives
open Adaptify

type Message =
    | CameraMessage  of FreeFlyController.Message
    | SetImageWidth  of int
    | SetImageHeight of int
    | SetTags        of string
    | TakeScreenshot
    | KeyDown        of key : Aardvark.Application.Keys

[<ModelType>]
type Model =
    {
        cameraState  : CameraControllerState

        imageSize    : V2i
        tags         : string[]
    }


