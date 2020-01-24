namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | LoadFiles of list<string>
    | SaveFile of string

[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
    }