namespace BoxTreeView.Model

open Aardvark.Base
open Aardvark.UI
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
}

/// Data shown per node in the tree view (both groups and box leaves).
[<ModelType>]
type TreeItemData = {
    label   : string
    isGroup : bool
}

type Message =
    | Camera         of FreeFlyController.Message
    | Select         of string * KeyModifiers
    | ScrollTo       of string
    | ToggleCollapse of string
    | Hover          of string option
    | TreeAction     of TreeView.Message<string>
    | GoldenLayout   of Golden.GoldenLayout.Message
    | MoveNode       of nodeId: string * targetParentId: string
    | RenameNode     of nodeId: string * newLabel: string
    | RemoveSelected
    | AddFolder
    | AddCube
    | ResetScene
    | ClearSelection

[<ModelType>]
type Model = {
    camera        : CameraControllerState
    boxes         : IndexList<VisibleBox>
    hoveredBox    : string option
    treeView      : TreeView<string, TreeItemData>
    golden        : Golden.GoldenLayout

    /// Pre-computed set of box IDs for O(1) box-vs-group lookup.
    [<NonAdaptive>]
    boxIds : HashSet<string>

    /// Group ID → label, used for path strings in the Move-to dropdown.
    [<NonAdaptive>]
    groupLabels : HashMap<string, string>
}
