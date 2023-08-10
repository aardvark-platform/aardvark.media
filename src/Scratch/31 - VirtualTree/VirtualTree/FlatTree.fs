namespace VirtualTree.Utilities

open System
open System.Collections.Generic
open System.Text
open FSharp.Data.Adaptive
open Aardvark.Base

[<AutoOpen>]
module internal DictionaryExtensions =

    type Dict<'K, 'V> with

        member inline x.TryFindV(key : 'K) =
            let mutable value = Unchecked.defaultof<_>
            if x.TryGetValue(key, &value) then ValueSome value
            else ValueNone


module internal ArraySegment =

    let inline mapArray (mapping : 'T1 -> 'T2) (segment : ArraySegment<'T1>) =
        Array.init segment.Count (fun i -> mapping segment.[i])

    let inline map (mapping : 'T1 -> 'T2) (segment : ArraySegment<'T1>) =
        ArraySegment (segment |> mapArray mapping)


[<Struct>]
type internal FlatNode =
    val Count  : int    // Number of nodes in subtree with the current node as root (inluding the node itself)
    val Depth  : int    // Depth of the node (0 for root)
    val Offset : int    // Offset from the parent, e.g. 1 for first child, count of first child for second child

    member inline x.IsLeaf =
        x.Count = 1

    new (count : int, depth : int, offset : int) =
        { Count  = count
          Depth  = depth
          Offset = offset }

[<Struct>]
type FlatItem<'T> =
    val Value  : 'T
    val Depth  : int
    val IsLeaf : bool

    new (value : 'T, depth : int, isLeaf : bool) =
        { Value  = value
          Depth  = depth
          IsLeaf = isLeaf }

/// Datastructure representing an arbitrary tree as a depth-first tour in a flat continguous array.
type FlatTree<'T> internal (nodes : ArraySegment<FlatNode>, values : ArraySegment<'T>, indices : Dict<'T, int>) =

    static let empty = FlatTree<'T>(Array.empty, Array.empty, Dict())
    static member Empty = empty

    internal new (nodes : FlatNode[], values : 'T[], indices : Dict<'T, int>) =
        FlatTree(ArraySegment nodes, ArraySegment values, indices)

    member private _.Nodes = nodes
    member private _.Values = values

    /// The root of the tree.
    member x.Root = values.[0]

    /// The number of nodes in the tree.
    member x.Count = values.Count

    /// Indicates if the tree is empty.
    member x.IsEmpty = x.Count = 0

    /// Returns the item at the given index in a depth-first traversal.
    member x.Item(index : int) =
        let n = nodes.[index]
        FlatItem(values.[index], n.Depth, n.IsLeaf)

    /// Returns the index of the given node if it exists.
    member x.IndexOf(value : 'T) =
        indices.TryFindV value

    /// Returns all values that are within the index range spanned by the given values.
    member x.Range(input : #seq<'T>) =
        let mutable minIndex = Int32.MaxValue
        let mutable maxIndex = Int32.MinValue

        for value in input do
            match indices.TryFindV value with
            | ValueSome index ->
                minIndex <- min minIndex index
                maxIndex <- max maxIndex index

            | _ -> ()

        if minIndex = Int32.MinValue then
            HashSet.Empty
        else
            HashSet.OfArrayRange(values.Array, values.Offset + minIndex, maxIndex - minIndex + 1)

    /// Returns whether the tree contains the given node.
    member x.Contains(value : 'T) =
        indices.ContainsKey value

    /// Returns whether the given node exists and is a leaf.
    member x.IsLeaf(value : 'T) =
        match indices.TryFindV value with
        | ValueSome i -> nodes.[i].IsLeaf
        | _ -> false

    /// Returns whether the given node exists and is the root.
    member x.IsRoot(value : 'T) =
        indices.TryFindV value = ValueSome 0

    /// Returns the parent of given the node, unless it is the root or does not exist.
    member x.Parent(value : 'T) =
        match indices.TryFindV value with
        | ValueSome i when i > 0 ->
            ValueSome values.[i - nodes.[i].Offset]
        | _ ->
            ValueNone

    /// Returns the values from the root to the given node.
    member x.RootPath(value : 'T) =
        match indices.TryFindV value with
        | ValueSome i ->
            let depth = nodes.[i].Depth
            let arr = Array.zeroCreate<'T> (depth + 1)

            let mutable i = i
            for j = 0 to depth do
                arr.[depth - j] <- values.[i]
                i <- i - nodes.[i].Offset

            arr

        | _ ->
            Array.empty

    /// Returns the value and its descendants, if it exists in the tree.
    member x.Descendants(value : 'T) =
        match indices.TryFindV value with
        | ValueSome i -> values.Slice(i, nodes.[i].Count)
        | _ -> ArraySegment.Empty

    /// Replaces the subtree with given root node with another subtree.
    member x.Replace(value : 'T, replacement : FlatTree<'T>) =
        match indices.TryFindV value with
        | ValueNone -> x
        | ValueSome 0 -> replacement
        | ValueSome index ->
            let node = nodes.[index]
            let countLeft = index
            let startRight = index + node.Count
            let countRight = nodes.Count - startRight
            let countTotal = countLeft + replacement.Count + countRight
            let countDelta = node.Count - replacement.Count

            // Compute new nodes
            let nodes' = Array.zeroCreate countTotal

            // Count of nodes on the root path of the replaced node have to be adjusted
            nodes.Slice(0, countLeft).CopyTo(nodes')

            if countDelta <> 0 then
                let mutable i = index - node.Offset
                for _ = 1 to node.Depth do
                    let n = nodes.[i]
                    nodes'.[i] <- FlatNode(n.Count - countDelta, n.Depth, n.Offset)
                    i <- i - n.Offset

            // Depth of replacement nodes has to be adjusted
            for i = 0 to replacement.Count - 1 do
                let n = replacement.Nodes.[i]
                let offset = if i = 0 then node.Offset else n.Offset
                nodes'.[countLeft + i] <- FlatNode(n.Count, n.Depth + node.Depth, offset)

            // Parent offsets of nodes right of the replaced subtree have to be adjusted
            // if the parent of that node is on the root path of the replaced subtree
            if countDelta = 0 then
                nodes.Slice(startRight, countRight).CopyTo(nodes', countLeft + replacement.Count)
            else
                for i = 0 to countRight - 1 do
                    let n = nodes.[startRight + i]

                    if startRight + i - n.Offset > index then
                        nodes'.[countLeft + replacement.Count + i] <- n
                    else
                        nodes'.[countLeft + replacement.Count + i] <- FlatNode(n.Count, n.Depth, n.Offset - countDelta)

            // Copy values
            let values' = Array.zeroCreate countTotal
            values.Slice(0, countLeft).CopyTo(values')
            replacement.Values.CopyTo(values', countLeft)
            values.Slice(startRight).CopyTo(values', countLeft + replacement.Count)

            // Compute indices
            let indices' = Dict(initialCapacity = countTotal)

            for i = 0 to countTotal - 1 do
                indices'.Add(values'.[i], i)

            FlatTree(nodes', values', indices')

    /// Deletes the given node and its descendants.
    member inline x.Delete(value : 'T) =
        x.Replace(value, FlatTree.Empty)

    member private x.SubTree(i : int) =
        let node = nodes.[i]
        let values = values.Slice(i, node.Count)

        let nodes =
            nodes.Slice(i, node.Count) |> ArraySegment.map (fun n ->
                FlatNode(n.Count, n.Depth - node.Depth, n.Offset)
            )

        let indices = Dict(initialCapacity = node.Count)
        for i = 0 to node.Count - 1 do
            indices.Add(values.[i], i)

        FlatTree(nodes, values, indices)

    /// Returns the subtree with the given value as root.
    member x.SubTree(value : 'T) =
        match indices.TryFindV value with
        | ValueNone -> empty
        | ValueSome 0 -> x
        | ValueSome i -> x.SubTree i

    member private x.Children(i : int) =
        let node = nodes.[i]

        let mutable count = 0
        let mutable j = i + 1

        while j < i + node.Count do
            j <- j + nodes.[j].Count
            inc &count

        count

    /// Returns all subtrees with the direct children collapsed.
    member x.Collapsed() =
        let result = ResizeArray(capacity = x.Count)

        for i = 0 to x.Count - 1 do
            let node = nodes.[i]

            if node.Count > 1 then
                let subtree =
                    let count = 1 + x.Children i

                    if node.Count = count then
                        x.SubTree i
                    else
                        let nodes' = Array.zeroCreate count
                        let values' = Array.zeroCreate count

                        nodes'.[0] <- FlatNode(count, 0, 0)
                        values'.[0] <- values.[i]

                        let mutable j = 1
                        let mutable k = i + 1

                        while k < i + node.Count do
                            nodes'.[j] <- FlatNode(1, 1, j)
                            values'.[j] <- values.[k]
                            k <- k + nodes.[k].Count
                            inc &j

                        let indices = Dict(initialCapacity = count)
                        for i = 0 to count - 1 do
                            indices.Add(values'.[i], i)

                        FlatTree(nodes', values', indices)

                result.Add <| struct (values.[i], subtree)

        HashMap.ofSeqV result

    override x.ToString() =
        if x.IsEmpty then "[]"
        else
            let sb = StringBuilder(nodes.Count)
            sb.Append (string values.[0]) |> ignore

            let mutable d = nodes.[0].Depth
            for i = 1 to x.Count - 1 do
                let di = nodes.[i].Depth

                if di > d then
                    sb.Append " [ " |> ignore
                elif di < d then
                    let cb = String.replicate (d - di) "]"
                    sb.Append $" {cb} " |> ignore
                else
                    sb.Append "; " |> ignore

                sb.Append (string values.[i]) |> ignore
                d <- di

            if x.Count > 1 then
                sb.Append " ]" |> ignore

            sb.ToString()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FlatTree =

    /// Returns the empty tree.
    let empty<'T> =
        FlatTree<'T>.Empty

    /// Creates a tree consisting of the given value.
    let singleton (value : 'T) =
        FlatTree<'T>(
            [| FlatNode(count = 1, depth = 0, offset = 0) |],
            [| value |],
            Dict [| KeyValuePair(value, 0) |]
        )

    /// Returns whether the given tree is empty.
    let inline isEmpty (tree : FlatTree<'T>) =
        tree.IsEmpty

    /// Returns the root of the tree.
    let inline root (tree : FlatTree<'T>) =
        tree.Root

    /// Returns the number of nodes in the tree.
    let inline count (tree : FlatTree<'T>) =
        tree.Count

    /// Builds a flat tree from the given hierarchy.
    let ofHierarchy (getChildren : 'T -> #seq<'T>) (root : 'T) =
        let nodes = ResizeArray<FlatNode>()
        let values = ResizeArray<'T>()
        let indices = Dict()

        let rec dft (parent : int) (depth : int) (value : 'T) =
            let index = values.Count

            nodes.Add <| FlatNode()
            values.Add value
            indices.[value] <- index

            for c in getChildren value do
                c |> dft index (depth + 1)

            nodes.[index] <- FlatNode(values.Count - index, depth, index - parent)

        root |> dft 0 0

        FlatTree(
            nodes.ToArray(),
            values.ToArray(),
            indices
        )

    /// Returns whether the given node exists and is a leaf.
    let inline isLeaf (value : 'T) (tree : FlatTree<'T>) =
        tree.IsLeaf value

    /// Returns whether the given node exists and is the root.
    let inline isRoot (value : 'T) (tree : FlatTree<'T>) =
        tree.IsRoot value

    /// Returns the index of the given node if it exists.
    let inline indexOf (value : 'T) (tree : FlatTree<'T>) =
        tree.IndexOf value

    /// Returns all values that are within the index range spanned by the given values.
    let inline range (input : #seq<'T>) (tree : FlatTree<'T>) =
        tree.Range input

    /// Returns whether the tree contains the given node.
    let inline contains (value : 'T) (tree : FlatTree<'T>) =
        tree.Contains value

    /// Returns the parent of given the node, unless it is the root or does not exist.
    let inline parent (value : 'T) (tree : FlatTree<'T>) =
        tree.Parent value

    /// Returns the values from the root to the given node.
    let inline rootPath (value : 'T) (tree : FlatTree<'T>) =
        tree.RootPath value

    /// Returns the value and its descendants, if it exists in the tree.
    let inline descendants (value : 'T) (tree : FlatTree<'T>) =
        tree.Descendants value

    /// Deletes the given node and its descendants.
    let inline delete (value : 'T) (tree : FlatTree<'T>) =
        tree.Delete value

    /// Replaces the subtree with given root node with another subtree.
    let inline replace (value : 'T) (replacement : FlatTree<'T>) (tree : FlatTree<'T>) =
        tree.Replace(value, replacement)

    /// Returns the subtree with the given value as root.
    let inline subTree (value : 'T) (tree : FlatTree<'T>) =
        tree.SubTree value

    /// Returns all subtrees with the direct children collapsed.
    let inline collapsed (tree : FlatTree<'T>) =
        tree.Collapsed()