namespace BoxTreeView.Model

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden
open FSharp.Data.Adaptive
open Adaptify
open TreeView.Model

[<ModelType>]
type VisibleBox = {
    [<NonAdaptive>]
    id       : string
    [<NonAdaptive>]
    name     : string
    geometry : Box3d
    color    : C4b
}

/// Data shown per node in the tree view (both groups and box leaves).
[<ModelType>]
type TreeItemData = {
    label   : string
    isGroup : bool
    color   : C4b
}

type Message =
    | Camera       of FreeFlyController.Message
    | Select       of string
    | Hover        of string option
    | TreeAction   of TreeView.Message<string>
    | GoldenLayout of Golden.GoldenLayout.Message

[<ModelType>]
type Model = {
    camera        : CameraControllerState
    boxes         : IndexList<VisibleBox>
    selectedBoxes : HashSet<string>
    hoveredBox    : string option
    treeView      : TreeView<string, TreeItemData>
    golden        : Golden.GoldenLayout

    /// Pre-computed set of box IDs for O(1) box-vs-group lookup.
    [<NonAdaptive>]
    boxIds : HashSet<string>
}
