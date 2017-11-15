namespace DragNDrop

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Trafos

type Drag = { PickPoint : V3d; Offset : V3d }

[<DomainType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<Drag>
    camera      : CameraControllerState
}

[<DomainType>]
type Scene = {
    transformation : Transformation
    camera         : CameraControllerState
}