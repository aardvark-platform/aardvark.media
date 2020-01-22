namespace Model

open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

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