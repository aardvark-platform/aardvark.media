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
            { tree with height = max tree.height height }

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
            | ValueSome i -> { tree with scrollTarget = i * tree.height.itemHeight }
            | _ -> tree

        | VirtualTree.Message.Collapse node ->
            let leaf = FlatTree.singleton node
            let subtree = tree.current |> FlatTree.subTree node

            { tree with
                current   = tree.current |> FlatTree.replace node leaf
                collapsed = tree.collapsed |> HashMap.add node subtree }

        | VirtualTree.Message.CollapseAll ->
            if tree.hierarchy.IsEmpty then tree
            else
                { tree with
                    current   = FlatTree.singleton tree.hierarchy.Root
                    collapsed = FlatTree.collapsed tree.hierarchy }

        | VirtualTree.Message.Uncollapse node ->
            let subtree = tree.collapsed.[node]

            { tree with
                current   = tree.current |> FlatTree.replace node subtree
                collapsed = tree.collapsed |> HashMap.remove node }

        | VirtualTree.Message.UncollapseAll ->
            { tree with
                current   = tree.hierarchy
                collapsed = HashMap.empty }

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
                let! itemHeight = tree.height.itemHeight

                if itemHeight = 0 then
                    let! elements = tree.current

                    // We do not know the item height yet, render two elements to determine it.
                    // Also we need the client height of the list, expand it to the max with a big virtual item.
                    if elements.Count > 0 then
                        yield virtualItem 9999

                        for i = 0 to (min elements.Count 2) - 1 do
                            yield item <| VirtualTree.Item(elements.[i], false)
                else
                    let! elements = tree.current
                    let! clientHeight = tree.height.clientHeight
                    let! scrollOffset = tree.scrollOffset

                    let first = ((scrollOffset / itemHeight) - border) |> clamp 0 (elements.Count - 1)
                    let last = (first + (clientHeight / itemHeight) + 2 * border) |> min (elements.Count - 1)

                    if first > 0 then
                        yield virtualItem (first * itemHeight)

                    for i = first to last do
                        let fi = elements.[i]
                        let ti = VirtualTree.Item(fi, tree.collapsed.ContainsKey fi.Value)
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