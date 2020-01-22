namespace DragNDrop

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Trafos

type Drag = { PickPoint : V3d; Offset : V3d }

[<ModelType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<Drag>
    camera      : CameraControllerState
}

[<ModelType>]
type Scene = {
    transformation : Transformation
    camera         : CameraControllerState
}