namespace TreeView.Model

open Aardvark.UI
open FSharp.Data.Adaptive
open Adaptify
open System

open VirtualTree.Model
open VirtualTree.Utilities

[<Flags>]
type Visibility =
    | Hidden       = 1uy
    | Visible      = 2uy
    | Intermediate = 3uy

[<ModelType>]
type TreeView<'Key, 'Value> =
    {
        tree       : VirtualTree<'Key>
        values     : HashMap<'Key, 'Value>
        hovered    : 'Key voption
        visibility : Visibility[]  // Visibility state for each flat tree node

        [<TreatAsValue>]
        selected : HashSet<'Key>   // Adaptify does not support generic HashSets

        [<NonAdaptive>]
        lastClick : 'Key voption   // For range select
    }

module TreeView =

    [<RequireQualifiedAccess>]
    type Message<'Key> =
        | Toggle of key: 'Key
        | Hover of key: 'Key
        | Unhover
        | Click of key : 'Key * modifiers: KeyModifiers
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
        { tree       = VirtualTree.empty
          values     = HashMap.empty
          visibility = Array.empty
          hovered    = ValueNone
          selected   = HashSet.empty
          lastClick  = ValueNone }

    let set (getChildren : 'Key -> #seq<'Key>) (values : HashMap<'Key, 'Value>) (root : 'Key) (tree : TreeView<'Key, 'Value>) =
        let flat = root |> FlatTree.ofHierarchy getChildren

        { empty with
            tree       = tree.tree |> VirtualTree.set flat
            values     = values
            visibility = Array.replicate flat.Count Visibility.Visible }

    let initialize (getChildren : 'Key -> #seq<'Key>) (values : HashMap<'Key, 'Value>) (root : 'Key) =
        let flat = root |> FlatTree.ofHierarchy getChildren

        { empty with
            tree       = VirtualTree.ofTree flat
            values     = values
            visibility = Array.replicate flat.Count Visibility.Visible }