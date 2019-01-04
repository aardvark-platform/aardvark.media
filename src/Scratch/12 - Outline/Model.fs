namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Message = 
    | Camera          of FreeFlyController.Message
    | CenterScene
    | Tick            of float
    | ChangeThickness of Numeric.Action
    | ToggleAnimation

[<DomainType>]
type Model = 
    {
        cameraState      : CameraControllerState
        trafo            : Trafo3d
        animationEnabled : bool
        thickness        : NumericInput
    }