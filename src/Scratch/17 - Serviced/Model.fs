namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | Camera of FreeFlyController.Message


[<ModelType>]
type Model = {
    value : int
    cameraState : CameraControllerState
}

type MasterMessage =
    | ResetAll
    | Nop

[<ModelType>]
type MasterModel = 
    {
        clients : HashMap<string,int>
    }

