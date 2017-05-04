namespace DragNDrop

open Aardvark.Base
open Aardvark.Base.Incremental

type Drag = { PickPoint : V3d; Offset : V3d }

[<DomainType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<Drag>
    camera      : Demo.TestApp.CameraControllerState
}

type Axis = X | Y | Z

type PickPoint =
    {
        point : V3d
        offset : V3d
        axis : Axis
    }

[<DomainType>]
type TranslateModel = { 
    trafo       : Trafo3d 
    hovered     : Option<Axis>
    grabbed     : Option<PickPoint>
    camera      : Demo.TestApp.CameraControllerState
}