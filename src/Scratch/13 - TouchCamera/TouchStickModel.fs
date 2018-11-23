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
        expo : bool
        cameraState : CameraControllerState
    }

module TouchStickModel =
    let initial =
        {
            expo = true
            rotStick = None
            movStick = None
            cameraState = FreeFlyController.initial
        }

type TouchStickMessage =
    | Camera of FreeFlyController.Message
    | MoveRotStick of TouchStickState
    | MoveMovStick of TouchStickState
    | EndRotStick
    | EndMovStick
    | SwitchExpo