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
                    { tree with scrollTarget = i * tree.height.itemHeight }
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
            onEvent "onscroll" [ "event.target.scrollTop" ] (
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
                "        itemHeight: getItemHeight(),"
                "        clientHeight: self.clientHeight"
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

        let virtualItem (height : int) =
            div [clazz "item virtual"; style $"height: {height}px; visibility: hidden"] []

        // Number of elements the window extends beyond the actual visible space
        let border = 5

        let elements =
            alist {
                let! showRoot = tree.showRoot
                let! itemHeight = tree.height.itemHeight
                let origin = if showRoot then 0 else 1

                if itemHeight = 0 then
                    let! elements = tree.current

                    // We do not know the item height yet, render two elements to determine it.
                    // Also we need the client height of the list, expand it to the max with a big virtual item.
                    if elements.Count > origin then
                        yield virtualItem 9999

                        for i = origin to (min elements.Count 2) - 1 do
                            let fi = elements.[i]
                            yield item <| VirtualTree.Item(fi.Value, fi.Depth - origin, VirtualTree.ItemKind.Uncollapsed)
                else
                    let! elements = tree.current
                    let! clientHeight = tree.height.clientHeight
                    let! scrollOffset = tree.scrollOffset

                    if elements.Count > origin then
                        let first = (origin + (scrollOffset / itemHeight) - border) |> clamp origin (elements.Count - 1)
                        let last = (first + (clientHeight / itemHeight) + 2 * border) |> min (elements.Count - 1)

                        if first > origin then
                            yield virtualItem ((first - origin) * itemHeight)

                        for i = first to last do
                            let fi = elements.[i]

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

                        if last < elements.Count - 1 then
                            yield virtualItem ((elements.Count - last - 1) * itemHeight)
            }

        let channels : (string * Channel) list = [
            "elementsCh", SignalChannel tree.current
            "scrollCh", AVal.channel tree.scrollTarget
        ]

        let attributes =
            let events = AttributeMap.ofList [ onUpdate; onScroll ]
            AttributeMap.union attributes events

        onBoot' channels bootJs (
            elements |> Incremental.div attributes
        )