namespace VirtualTree.App

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive

open VirtualTree.Model
open VirtualTree.Utilities

module VirtualTree =

    // Channel that triggers whenever the adaptive value changes (ignoring the actual value)
    type private SignalChannelReader<'T>(value : aval<'T>) =
        inherit ChannelReader()

        override x.Release() = ()
        override x.ComputeMessages t =
            value.GetValue t |> ignore
            [ "0" ]

    type private SignalChannel<'T>(value : aval<'T>) =
        inherit Channel()
        override x.GetReader() = new SignalChannelReader<_>(value) :> ChannelReader


    let rec update (message : VirtualTree.Message<'T>) (tree : VirtualTree<'T>) =
        match message with
        | VirtualTree.Message.OnResize height ->
            let height = { height with itemHeight = max height.itemHeight tree.height.itemHeight }
            { tree with height = height }

        | VirtualTree.Message.OnScroll offset ->
            { tree with scrollOffset = offset }

        | VirtualTree.Message.ScrollTo target ->
            let path = tree.hierarchy |> FlatTree.rootPath target

            let tree =
                (tree, path) ||> Array.fold (fun tree node ->
                    if tree.collapsed.ContainsKey node then
                        tree |> update (VirtualTree.Message.Uncollapse node)
                    else
                        tree
                )

            match tree.current |> FlatTree.tryIndexOf target with
            | ValueSome i ->
                let i = if tree.showRoot then i else i - 1

                if i > 0 then
                    { tree with scrollTarget = float i * tree.height.itemHeight }
                else
                    tree
            | _ ->
                tree

        | VirtualTree.Message.Collapse node ->
            let leaf = FlatTree.singleton node
            let subtree = tree.current |> FlatTree.subTree node

            { tree with
                current   = tree.current |> FlatTree.replace node leaf
                collapsed = tree.collapsed |> HashMap.add node subtree }

        | VirtualTree.Message.CollapseAll ->
            if tree.hierarchy.IsEmpty then tree
            else
                let collapsed =
                    { tree with
                        current   = FlatTree.singleton tree.hierarchy.Root
                        collapsed = FlatTree.collapsed tree.hierarchy }

                if tree.showRoot then collapsed
                else
                    let root = tree.hierarchy.Root
                    collapsed |> update (VirtualTree.Message.Uncollapse root)

        | VirtualTree.Message.Uncollapse node ->
            match tree.collapsed |> HashMap.tryFindV node with
            | ValueSome subtree ->
                { tree with
                    current   = tree.current |> FlatTree.replace node subtree
                    collapsed = tree.collapsed |> HashMap.remove node }

            | _ ->
                tree

        | VirtualTree.Message.UncollapseAll ->
            { tree with
                current   = tree.hierarchy
                collapsed = HashMap.empty }

        | VirtualTree.Message.ToggleRoot ->
            let tree = { tree with showRoot = not tree.showRoot }

            if tree.showRoot || tree.hierarchy.IsEmpty then tree
            else
                let root = tree.hierarchy.Root
                tree |> update (VirtualTree.Message.Uncollapse root)


    let view (message : VirtualTree.Message<'T> -> 'msg)
             (itemNode : VirtualTree.Item<'T> -> DomNode<'msg>)
             (attributes : AttributeMap<'msg>) (tree : AdaptiveVirtualTree<'T, _, _>) : DomNode<'msg> =

        let onUpdate =
            onEvent "onupdate" [ ] (
                List.head >> Pickler.unpickleOfJson >> VirtualTree.Message.OnResize >> message
            )

        let onScroll =
            onEvent "onscroll" [ "event.target.scrollTop.toFixed(10)" ] (
                List.head >> Pickler.unpickleOfJson >> VirtualTree.Message.OnScroll >> message
            )

        let bootJs =
            String.concat "" [
                "const self = $('#__ID__')[0];"

                "const getItemHeight = () => {"
                "    const $item = $(self).children('.item:not(.virtual):first');"
                "    return $item.length > 0 ? $item.outerHeight(true) : 0;"
                "};"

                "const updateHeight = () => {"
                "    const height = {"
                "        itemHeight: getItemHeight().toFixed(10),"
                "        clientHeight: self.clientHeight.toFixed(10)"
                "    };"

                "    aardvark.processEvent('__ID__', 'onupdate', height);"
                "};"

                "const scrollTo = (offset) => {"
                "    self.scrollTo({ top: offset, behavior: 'smooth'});"
                "};"
                "scrollCh.onmessage = scrollTo;"

                // Client and item height may change, update it when the elements or the size change
                "elementsCh.onmessage = updateHeight;"
                "const resizeObserver = new ResizeObserver(updateHeight);"
                "resizeObserver.observe(self);"
            ]

        let item (item : VirtualTree.Item<'T>) =
            itemNode item

        let virtualItem (height : aval<float>) =
            alist {
                let! height = height
                if height > 0.0 then
                    yield div [clazz "item virtual"; style $"height: {height}px; visibility: hidden"] []
            }

        // Number of elements the window extends beyond the actual visible space
        let border = 5

        let range =
            adaptive {
                let! itemHeight = tree.height.itemHeight
                let! elements = tree.current
                let! clientHeight = tree.height.clientHeight
                let! scrollOffset = tree.scrollOffset
                let! showRoot = tree.showRoot
                let origin = if showRoot then 0 else 1

                let first = (origin + (int (scrollOffset / itemHeight)) - border) |> clamp origin (elements.Count - 1)
                let last = (first + (int (clientHeight / itemHeight)) + 2 * border) |> min (elements.Count - 1)

                return Range1i(first, last)
            }

        let itemsInView =
            let deltas = ResizeArray()
            let mutable lastTree = Unchecked.defaultof<_>
            let mutable lastRange = Range1i.Invalid

            AList.custom (fun token curr ->
                deltas.Clear()
                let range = range.GetValue token
                let elements = tree.current.GetValue token
                let indexed = curr |> IndexList.toArrayIndexed

                if not <| obj.ReferenceEquals(lastTree, elements) then
                    // Tree elements changed, simply remove and readd everything
                    for i, _ in indexed do
                        deltas.Add(i, Remove)

                    let mutable index = Index.zero
                    for i = range.Min to range.Max do
                        index <- Index.after index
                        deltas.Add (index, Set elements.[i])
                else
                    // View changed, compute deltas
                    for i = lastRange.Min to min (range.Min - 1) lastRange.Max do
                        let index, _ = indexed.[i - lastRange.Min]
                        deltas.Add(index, Remove)

                    for i = max (range.Max + 1) lastRange.Min to lastRange.Max do
                        let index, _ = indexed.[i - lastRange.Min]
                        deltas.Add(index, Remove)

                    let mutable index = IndexList.firstIndex curr
                    for i = min range.Max (lastRange.Min - 1) downto range.Min do
                        index <- Index.before index
                        deltas.Add(index, Set elements.[i])

                    let mutable index = IndexList.lastIndex curr
                    for i = max range.Min (lastRange.Max + 1) to range.Max do
                        index <- Index.after index
                        deltas.Add(index, Set elements.[i])
                            
                lastTree <- elements
                lastRange <- range
                IndexListDelta.ofSeq deltas
            )

        let items =
            alist {
                let! showRoot = tree.showRoot
                let! itemHeight = tree.height.itemHeight
                let! elements = tree.current
                let origin = if showRoot then 0 else 1

                if elements.Count > origin then
                    if itemHeight = 0 then
                        // We do not know the item height yet, render two elements to determine it.
                        // Also we need the client height of the list, expand it to the max with a big virtual item.
                        yield! virtualItem (AVal.constant 9999)

                        for i = origin to (min elements.Count 2) - 1 do
                            let fi = elements.[i]
                            yield item <| VirtualTree.Item(fi.Value, fi.Depth - origin, VirtualTree.ItemKind.Uncollapsed)
                    else
                        let vh1 = range |> AVal.map (fun r -> max 0.0 (float (r.Min - origin) * itemHeight))
                        let vh2 = range |> AVal.map (fun r -> max 0.0 (float (elements.Count - r.Max - 1) * itemHeight))

                        yield! virtualItem vh1

                        for fi in itemsInView do
                            let kind =
                                if fi.IsLeaf then
                                    if tree.collapsed.ContainsKey fi.Value then
                                        VirtualTree.ItemKind.Collapsed
                                    else
                                        VirtualTree.ItemKind.Leaf
                                else
                                    VirtualTree.ItemKind.Uncollapsed

                            let ti = VirtualTree.Item(fi.Value, fi.Depth - origin, kind)
                            yield item ti

                        yield! virtualItem vh2
            }

        let channels : (string * Channel) list = [
            "elementsCh", SignalChannel tree.current
            "scrollCh", AVal.channel tree.scrollTarget
        ]

        let attributes =
            let events = AttributeMap.ofList [ onUpdate; onScroll ]
            AttributeMap.union attributes events

        onBoot' channels bootJs (
            items |> Incremental.div attributes
        )