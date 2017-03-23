namespace Demo.TestApp

open Aardvark.Base
open Aardvark.Base.Incremental

type ClientLocalAttribute() = inherit System.Attribute()

[<DomainType>]
type Model =
    {
        lastName    : Option<string>
        elements    : plist<string>
        hasD3Hate   : bool
        boxScale    : float
        boxHovered  : bool
        dragging    : bool
    }





[<DomainType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        look : bool
        zoom : bool
        pan : bool

        moveVec : V3i
        lastTime : Option<float>
    }
