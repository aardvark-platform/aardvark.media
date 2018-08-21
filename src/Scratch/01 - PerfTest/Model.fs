namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | Tick of float
    | ToggleAnimation

    | SetUpdateLoad of Aardvark.UI.Numeric.Action
    | SetGpuLoad    of Aardvark.UI.Numeric.Action
    | SetModLoad    of Aardvark.UI.Numeric.Action

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
        trafo : Trafo3d
        animationEnabled : bool

        gpuLoad : NumericInput
        modLoad : NumericInput
        updateLoad : NumericInput
    }