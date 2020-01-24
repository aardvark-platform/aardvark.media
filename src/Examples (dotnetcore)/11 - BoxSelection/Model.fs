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
    | AddBox
    | RemoveBox
    | ClearSelection
    | Nop

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
    boxesMap : HashMap<string,VisibleBox>

    boxHovered : option<string>
    selectedBoxes : HashSet<string>
}