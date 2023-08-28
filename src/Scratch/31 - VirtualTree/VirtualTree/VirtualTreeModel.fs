namespace VirtualTree.Model

open Adaptify
open FSharp.Data.Adaptive
open VirtualTree.Utilities

[<Struct; ModelType>]
type VirtualHeight =
    {
        itemHeight   : int      // Height of an element in pixels
        clientHeight : int      // Height of the whole container in pixels
    }

[<ModelType>]
type VirtualTree<'T> =
    {
        height : VirtualHeight
        scrollOffset : int      // Scroll position in pixels
        scrollTarget : int      // Target scroll position in pixels

        current   : FlatTree<'T>    // Current state of the tree to be rendered
        hierarchy : FlatTree<'T>    // Full tree hiearchy including collapsed subtrees

        [<NonAdaptive>]
        collapsed : HashMap<'T, FlatTree<'T>>  // Collapsed subtrees

        showRoot : bool     // Whether the root node should be displayed (false for forest view)
    }


module VirtualTree =

    type ItemKind =
        | Leaf = 0
        | Collapsed = 1
        | Uncollapsed = 2

    [<Struct>]
    type Item<'T> =
        val Value : 'T
        val Depth : int
        val Kind  : ItemKind

        member inline x.IsRoot =
            x.Depth = 0

        member inline x.IsLeaf =
            x.Kind = ItemKind.Leaf

        member inline x.IsCollapsed =
            x.Kind = ItemKind.Collapsed

        new (value : 'T, depth : int, kind : ItemKind) =
            { Value = value
              Depth = depth
              Kind  = kind }

    [<RequireQualifiedAccess>]
    type Message<'T> =
        | OnResize of height: VirtualHeight
        | OnScroll of offset: int
        | ScrollTo of target: 'T
        | Collapse of key: 'T
        | CollapseAll
        | Uncollapse of key: 'T
        | UncollapseAll
        | ToggleRoot

    let inline ofTree (tree : FlatTree<'T>) =
        { height       = Unchecked.defaultof<_>
          scrollOffset = 0
          scrollTarget = -1

          current = tree
          hierarchy = tree

          collapsed = HashMap.empty

          showRoot = false }

    let inline empty<'T> : VirtualTree<'T> =
        ofTree FlatTree.empty

    let inline set (flat : FlatTree<'T>) (tree : VirtualTree<'T>) =
        { tree with
            current = flat
            hierarchy = flat
            collapsed = HashMap.empty }