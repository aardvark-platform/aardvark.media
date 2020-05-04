namespace Model

open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

[<ModelType>]
type Model = 
    {   
        cameraState : CameraControllerState
    }

[<ModelType>]
type ServerModel = 
    {   
        value : int
    }