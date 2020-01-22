namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives

type DragInfo = {
    absolutePosition : V2d
    delta : V2d
}

type DragMode = Vertical = 0 | Horizontal = 1 | Unrestricted = 2

type Message = 
    | Drag of DragInfo
    | StopDrag of DragInfo
    | SetDragMode of DragMode
    | SetStepSize of Numeric.Action


[<ModelType>]
type Model = 
    {
        dragInfo   : Option<DragInfo>
        pos      : V2d

        dragMode : DragMode
        stepSize : NumericInput
    }