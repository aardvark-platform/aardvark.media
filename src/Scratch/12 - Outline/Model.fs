namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | Tick of float
    | ToggleAnimation

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
        trafo : Trafo3d
        animationEnabled : bool
    }