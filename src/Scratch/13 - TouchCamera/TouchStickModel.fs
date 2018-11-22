namespace TouchCamera

open System
open Aardvark.Base
open Aardvark.Base.Incremental


open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.TouchStick


[<DomainType>]
type TouchStickModel =
    {
        rotStick   : Option<TouchStickState>
        movStick   : Option<TouchStickState>
        cameraState : CameraControllerState
    }

module TouchStickModel =
    let initial =
        {
            rotStick = None
            movStick = None
            cameraState = FreeFlyController.initial
        }

type TouchStickMessage =
    | Camera of FreeFlyController.Message
    | StartMovStick of TouchStickState
    | StartRotStick of TouchStickState
    | MoveRotStick of TouchStickState
    | MoveMovStick of TouchStickState
    | EndRotStick
    | EndMovStick