namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

type CameraControllerMessage = 
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Move of V2i
        | StepTime
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Blur

[<DomainType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        look : bool
        zoom : bool
        pan : bool

        forward : bool
        backward : bool
        left : bool
        right : bool
        moveVec : V3i
        orbitCenter : Option<V3d>
        lastTime : Option<float>

        sensitivity : float

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 
    }

