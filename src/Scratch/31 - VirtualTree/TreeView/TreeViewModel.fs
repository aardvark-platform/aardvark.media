namespace TreeView.Model

open FSharp.Data.Adaptive
open Adaptify

open VirtualTree.Model
open VirtualTree.Utilities

[<ModelType>]
type TreeView<'Key, 'Value> =
    {
        tree     : VirtualTree<'Key>
        values   : HashMap<'Key, 'Value>
        hovered  : 'Key voption
        selected : 'Key voption
    }

module TreeView =

    [<RequireQualifiedAccess>]
    type Message<'Key> =
        | Hover of key: 'Key
        | Unhover
        | Select of key : 'Key
        | Virtual of VirtualTree.Message<'Key>

        static member inline ScrollTo(target : 'Key) =
            Virtual <| VirtualTree.Message.ScrollTo target

        static member inline Collapse(key : 'Key) =
            Virtual <| VirtualTree.Message.Collapse key

        static member inline CollapseAll : Message<'Key> =
            Virtual <| VirtualTree.Message.CollapseAll

        static member inline Uncollapse(key : 'Key) =
            Virtual <| VirtualTree.Message.Uncollapse key

        static member inline UncollapseAll : Message<'Key> =
            Virtual <| VirtualTree.Message.UncollapseAll

    let empty<'Key, 'Value> : TreeView<'Key, 'Value> =
        { tree     = VirtualTree.empty
          values   = HashMap.empty
          hovered  = ValueNone
          selected = ValueNone }

    let set (getChildren : 'Key -> #seq<'Key>) (values : HashMap<'Key, 'Value>) (root : 'Key) (tree : TreeView<'Key, 'Value>) =
        let flat = root |> FlatTree.ofHierarchy getChildren

        { empty with
            tree = tree.tree |> VirtualTree.set flat
            values = values }

    let initialize (getChildren : 'Key -> #seq<'Key>) (values : HashMap<'Key, 'Value>) (root : 'Key) =
        let flat = root |> FlatTree.ofHierarchy getChildren

        { empty with
            tree = VirtualTree.ofTree flat
            values = values }