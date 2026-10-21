namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

open RenderingParametersModel
open Adaptify

type BoxSelectionDemoAction =
    | CameraMessage    of FreeFlyController.Message
    | RenderingAction  of RenderingParametersModel.Action
    | Select of string
    | Enter of string
    | Exit
    | AddBoxes of count : int * color : option<C4b>
    | SetColor of ids : list<string> * color : C4b
    | SetSelection of ids : list<string>
    | RemoveBoxes of ids : list<string>
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

    boxes : IndexList<VisibleBox>

    boxHovered : option<string>
    selectedBoxes : HashSet<string>
}
