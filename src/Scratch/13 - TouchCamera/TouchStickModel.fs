namespace TouchCamera

open System
open Aardvark.Base
open Aardvark.Base.Incremental


open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.TouchStick


[<DomainType>]
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