namespace Demo.TestApp

open Aardvark.Base
open Aardvark.Base.Incremental

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
        moveDirection : V3d
        dragStart : V2i
        look : bool
        zoom : bool
        pan : bool
    }