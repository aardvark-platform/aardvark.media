namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

open RenderingParametersModel
open Adaptify
open SortedHashMap

type BoxSelectionDemoAction =
    | CameraMessage    of FreeFlyController.Message     
    | RenderingAction  of RenderingParametersModel.Action
    | Select of string     
    | Enter of string
    | Exit of string
    | AddBox
    | RemoveBox
    | ClearSelection
    | SetTestValue of string * float
    | SetSorting of string * float

[<ModelType>]
type VisibleBox = {
    geometry : Box3d
    [<NonAdaptive>]
    color    : C4b    
    isSelected: bool
    isHovered : bool

    [<NonAdaptive>]
    id : string
    //[<NonAdaptive>]
    sorting : int
    testValue : float
}

[<ModelType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState    
    rendering : RenderingParameters

    //[<NonAdaptive>]
    //boxesHelper: SortedHashMap<string, VisibleBox>
    //boxesSortedValues : IndexList<VisibleBox>
    //boxesSortedKeys : IndexList<string>
    boxesMap : HashMap<string,VisibleBox>

    //boxHovered : option<string>
    //selectedBoxes : HashSet<string>
}