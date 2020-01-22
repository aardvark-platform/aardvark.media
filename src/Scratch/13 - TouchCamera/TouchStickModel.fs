namespace TouchCamera

open System
open Aardvark.Base
open FSharp.Data.Adaptive


open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.TouchStick


[<ModelType>]
type TouchStickModel =
    {
        cameraState : CameraControllerState
    }

module TouchStickModel =
    let initial =
        {
            cameraState = FreeFlyController.initial
        }

type TouchStickMessage =
    | Camera of FreeFlyController.Message
    | SwitchExpo