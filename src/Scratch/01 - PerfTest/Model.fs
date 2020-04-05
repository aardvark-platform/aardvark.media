namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | Tick of float
    | ToggleAnimation

    | SetUpdateLoad of Aardvark.UI.Numeric.Action
    | SetGpuLoad    of Aardvark.UI.Numeric.Action
    | SetModLoad    of Aardvark.UI.Numeric.Action

[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
        trafo : Trafo3d
        animationEnabled : bool

        gpuLoad : NumericInput
        modLoad : NumericInput
        updateLoad : NumericInput
    }