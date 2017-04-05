namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental

type ClientLocalAttribute() = inherit System.Attribute()

[<DomainType>]
type Model =
    {
        boxHovered      : bool
        dragging        : bool
        lastName        : Option<string>
        elements        : plist<string>
        hasD3Hate       : bool
        boxScale        : float
    }

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


        lastTime : Option<float>

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 

    }
