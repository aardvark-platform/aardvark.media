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



type Pose =
    {
        position : V3d
        rotation : Rot3d
        scale    : V3d
    } with
        member x.Trafo = 
            let rot = Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse)
            Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position

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