namespace TreeView.App

open Aardvark.UI
open FSharp.Data.Adaptive
open System

open TreeView.Model
open VirtualTree.Model
open VirtualTree.App
open VirtualTree.Utilities

module TreeView =

    module private ArraySegment =

        let inline contains (value : 'T) (segment : ArraySegment<'T>) =
            let mutable state = false
            let mutable i = 0

            while not state && i < segment.Count do
                state <- value = segment.[i]
                i <- i + 1

            state

    let update (message : TreeView.Message<'Key>) (model : TreeView<'Key, 'Value>) =
        match message with
        | TreeView.Message.Hover key ->
            { model with hovered = ValueSome key }

        | TreeView.Message.Unhover ->
            { model with hovered = ValueNone }

        | TreeView.Message.Select key ->
            let s = if model.selected = ValueSome key then ValueNone else ValueSome key
            { model with selected = s }

        | TreeView.Message.Virtual msg ->
            { model with tree = model.tree |> VirtualTree.update msg }

    let view (message : TreeView.Message<'Key> -> 'msg)
             (itemNode : 'Key -> 'primValue -> DomNode<'msg>)
             (model : AdaptiveTreeView<'Key, 'primKey, 'aKey, 'Value, 'primValue, 'aValue>) : DomNode<'msg> =

        let descendants (node : aval<'Key voption>) =
            adaptive {
                let! tree = model.tree.current
                match! node with
                | ValueSome h -> return tree |> FlatTree.descendants h
                | _ -> return ArraySegment.Empty
            }

        let hoverParent =
            adaptive {
                let! tree = model.tree.current
                match! model.hovered with
                | ValueSome h -> return tree |> FlatTree.parent h
                | _ -> return ValueNone
            }

        let hovered = descendants model.hovered
        let selected = descendants model.selected

        let itemNode (item : VirtualTree.Item<'Key>) =
            let value = model.values |> AMap.find item.Value
            let indent = item.Depth * 16

            let collapseIcon =
                let icon = if item.IsCollapsed then "caret right" else "caret down"

                if item.IsLeaf then
                    i [ clazz $"{icon} icon"; style "visibility: hidden" ] []
                else
                    let collapseMessage =
                        if item.IsCollapsed then
                            TreeView.Message.Uncollapse item.Value
                        else
                            TreeView.Message.Collapse item.Value
                        |> message

                    i [ clazz $"{icon} link icon"; style "color: white"; onClick (fun _ -> collapseMessage) ] []

            let attributes =
                AttributeMap.ofAMap <| amap {
                    let! hoverParent = hoverParent
                    let childHovered = hoverParent |> ValueOption.contains item.Value

                    let! hovered = hovered
                    let hovered = hovered |> ArraySegment.contains item.Value

                    let! selected = selected
                    let selected = selected |> ArraySegment.contains item.Value

                    if childHovered then
                        yield clazz "item parent"
                    else
                        yield
                            match hovered, selected with
                            | true, true  -> clazz "item hovered selected"
                            | true, false -> clazz "item hovered"
                            | false, true -> clazz "item selected"
                            | _           -> clazz "item"

                    yield style $"display: flex; justify-content: flex-start; align-items: center; padding-left: {indent}px"
                    yield onMouseEnter (fun _ -> message <| TreeView.Message.Hover item.Value)
                    yield onMouseLeave (fun _ -> message TreeView.Message.Unhover)
                    yield onMouseDoubleClick (fun _ -> message <| TreeView.Message.Select item.Value)
                }

            Incremental.div attributes <| alist {
                let! v = value
                yield collapseIcon
                yield itemNode item.Value v
            }

        let dependencies =
            [ { kind = Stylesheet; name = "treeview"; url = "./resources/TreeView.css" } ]

        let attributes =
            AttributeMap.ofList [
                clazz "ui inverted divided list treeview"
                style "margin: 10px; padding: 5px; height: 100%; max-height: 400px; overflow-x: hidden; overflow-y: auto; border-style: solid; border-width: 1px; border-color: gray"
                onMouseLeave (fun _ -> message TreeView.Message.Unhover)
            ]

        require dependencies (
            model.tree |> VirtualTree.view (TreeView.Message.Virtual >> message) itemNode attributes
        )