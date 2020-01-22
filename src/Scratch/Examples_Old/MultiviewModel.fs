namespace Examples.MultiviewModel

open Aardvark.Base                 
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

[<ModelType>]
type Model = { 
    camera1 : CameraControllerState 
    camera2 : CameraControllerState 
    camera3 : CameraControllerState
}

type Message = 
    | CameraMessage1 of CameraControllerMessage
    | CameraMessage2 of CameraControllerMessage
    | CameraMessage3 of CameraControllerMessage
    | SelectFiles of list<string>