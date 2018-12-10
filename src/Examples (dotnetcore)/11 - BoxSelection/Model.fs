namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

open RenderingParametersModel

type BoxSelectionDemoAction =
    | CameraMessage    of FreeFlyController.Message     
    | RenderingAction  of RenderingParametersModel.Action
    | Select of string     
    | Enter of string
    | Exit  
    | AddBox
    | RemoveBox
    | ClearSelection

[<DomainType>]
type VisibleBox = {
    geometry : Box3d
    color    : C4b    

    [<NonIncremental; PrimaryKey>]
    id : string
}

[<DomainType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState    
    rendering : RenderingParameters

    boxes : plist<VisibleBox>
    boxesSet : hset<VisibleBox>
    boxesMap : hmap<string,VisibleBox>

    boxHovered : option<string>
    selectedBoxes : hset<string>
}