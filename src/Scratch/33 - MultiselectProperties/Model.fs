namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

open RenderingParametersModel
open Adaptify

type MultiselectPropertiesAction =
    | CameraMessage    of FreeFlyController.Message     
    | RenderingAction  of RenderingParametersModel.Action
    | Select of string     
    | Enter of string
    | Exit  
    | AddBox
    | RemoveBox
    | ClearSelection
    | SetBoxColor of string * C4b
    | GoldenLayoutMsg of GoldenLayout.Message

[<ModelType>]
type VisibleBox = {
    geometry : Box3d
    color    : C4b    

    [<NonAdaptive>]
    id : string
}

[<ModelType>]
type MultiselectPropertiesModel = {
    camera : CameraControllerState    
    rendering : RenderingParameters

    boxes : IndexList<VisibleBox>
    boxesMap : HashMap<string,VisibleBox>

    boxHovered : option<string>
    selectedBoxes : HashSet<string>
    lastSelected : option<string>

    golden : GoldenLayout
}
