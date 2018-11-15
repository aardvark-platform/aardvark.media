namespace TouchStick

open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI.Primitives

type TouchStickState =
    {
        distance : float
        angle : float
    }

[<DomainType>]
type TouchStickModel =
    {
        leftstick : Option<TouchStickState>  
        ritestick : Option<TouchStickState>  

        cameraState : CameraControllerState
    }

module TouchStickModel =
    let initial =
        {
            leftstick = None
            ritestick = None
            cameraState = FreeFlyController.initial
        }

type TouchStickMessage =
    | LeftTouchStart of TouchStickState
    | LeftTouchUpdate of TouchStickState
    | LeftTouchEnd
    | RiteTouchStart of TouchStickState
    | RiteTouchUpdate of TouchStickState
    | RiteTouchEnd
    | Camera of FreeFlyController.Message