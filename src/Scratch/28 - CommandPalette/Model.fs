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
    | Select of int
    | Enter of int
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
}

[<ModelType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState
    rendering : RenderingParameters
    palette : PaletteState
    boxes : HashMap<int, VisibleBox>
    boxHovered : int option
    selectedBoxes : HashSet<int>
}
