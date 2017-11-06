namespace DragNDrop

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Drag = { PickPoint : V3d; Offset : V3d }

[<DomainType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<Drag>
    camera      : CameraControllerState
}

type Axis = X | Y | Z

type PickPoint =
    {
        offset : float
        axis   : Axis
        hit : V3d
    }


type TrafoMode =
    | Local  = 0
    | Global = 1

[<DomainType>]
type Transformation = { 
    trafo         : Trafo3d 
    workingTrafo  : Trafo3d
    pivotTrafo    : Trafo3d
    mode          : TrafoMode
    //pivotLocation    : V3d
    hovered       : Option<Axis>
    grabbed       : Option<PickPoint>
}

[<DomainType>]
type Scene = {
    transformation : Transformation
    camera         : CameraControllerState
}