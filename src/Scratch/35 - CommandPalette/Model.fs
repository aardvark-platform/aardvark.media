namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

open RenderingParametersModel
open CommandPalette
open Adaptify

type BoxSelectionDemoAction =
    | CameraMessage    of FreeFlyController.Message
    | RenderingAction  of RenderingParametersModel.Action
    | PaletteAction    of PaletteMessage
    | Select of string
    | Enter of string
    | Exit
    | AddBox
    | RemoveBox
    | RemoveSelected
    | ClearSelection
    | SelectAll
    | InvertSelection
    | ResetCamera

[<ModelType>]
type VisibleBox = {
    geometry : Box3d
    color    : C4b

    [<NonAdaptive>]
    id : string
}

[<ModelType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState
    rendering : RenderingParameters
    palette : PaletteState

    boxes : IndexList<VisibleBox>

    boxHovered : option<string>
    selectedBoxes : HashSet<string>
}
