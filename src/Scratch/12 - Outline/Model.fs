namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives

type Message = 
    | Camera          of FreeFlyController.Message
    | CenterScene
    | Tick            of float
    | ChangeThickness of Numeric.Action
    | ToggleAnimation

[<ModelType>]
type Model = 
    {
        cameraState      : CameraControllerState
        trafo            : Trafo3d
        animationEnabled : bool
        thickness        : NumericInput
    }