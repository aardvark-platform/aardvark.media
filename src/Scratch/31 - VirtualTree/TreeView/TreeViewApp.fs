namespace TreeView.App

open Aardvark.UI
open FSharp.Data.Adaptive

open TreeView.Model
open VirtualTree.Model
open VirtualTree.App
open VirtualTree.Utilities

module TreeView =

    module private ArraySegment =
        open System

        let inline contains (value : 'T) (segment : ArraySegment<'T>) =
            let mutable state = false
            let mutable i = 0

            while not state && i < segment.Count do
                state <- value = segment.[i]
                i <- i + 1

            state

    [<AutoOpen>]
    module private Events =

        let disablePropagation event =
            sprintf "$('#__ID__').on('%s', function(e) { e.stopPropagation(); } ); " event

        let onClickModifiers (cb : KeyModifiers -> 'msg) =
            onEvent "onclick" ["{ shift: event.shiftKey, alt: event.altKey, ctrl: event.ctrlKey  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let update (message : TreeView.Message<'Key>) (model : TreeView<'Key, 'Value>) =
        match message with
        | TreeView.Message.Hover key ->
            { model with hovered = ValueSome key }

        | TreeView.Message.Unhover ->
            { model with hovered = ValueNone }

        | TreeView.Message.Click (key, modifiers) ->
            if modifiers.ctrl then
                if model.selected |> HashSet.contains key then
                    { model with
                        selected  = model.selected |> HashSet.remove key
                        lastClick = ValueSome key }
                else
                    { model with
                        selected  = model.selected |> HashSet.add key
                        lastClick = ValueSome key }

            elif modifiers.shift then
                let anchor = model.lastClick |> ValueOption.defaultValue model.tree.hierarchy.Root
                { model with selected = model.tree.hierarchy |> FlatTree.range [| anchor; key |] }

            else
                { model with
                    selected  = HashSet.single key
                    lastClick = ValueSome key }

        | TreeView.Message.Virtual msg ->
            { model with tree = model.tree |> VirtualTree.update msg }

    let view (message : TreeView.Message<'Key> -> 'msg)
             (itemNode : 'Key -> 'primValue -> DomNode<'msg>)
             (model : AdaptiveTreeView<'Key, 'primKey, 'aKey, 'Value, 'primValue, 'aValue>) : DomNode<'msg> =

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

                    onBoot (disablePropagation "click") (
                        i [ clazz $"{icon} link icon"; style "color: white"; onClick (fun _ -> collapseMessage) ] []
                    )

            let attributes =
                AttributeMap.ofAMap <| amap {
                    let! hovered = model.hovered
                    let hovered = hovered |> ValueOption.contains item.Value

                    let! selected = model.selected
                    let selected = selected |> HashSet.contains item.Value

                    let classes =
                        [ "item"
                          if hovered then "hovered"
                          if selected then "selected" ]
                        |> String.concat " "

                    yield clazz classes
                    yield style $"display: flex; justify-content: flex-start; align-items: center; padding: 5px; padding-left: {indent + 5}px"
                    yield onMouseEnter (fun _ -> message <| TreeView.Message.Hover item.Value)
                    yield onMouseLeave (fun _ -> message TreeView.Message.Unhover)
                    yield onClickModifiers (fun modifiers -> message <| TreeView.Message.Click (item.Value, modifiers))
                }

            Incremental.div attributes <| alist {
                let! v = value
                yield collapseIcon
                yield itemNode item.Value v
            }

        let dependencies =
            [ { kind = Stylesheet; name = "treeview"; url = "resources/TreeView/TreeView.css" } ]

        let attributes =
            AttributeMap.ofList [
                clazz "ui inverted divided list treeview"
                style "margin: 10px; height: 100%; max-height: 400px; overflow-x: hidden; overflow-y: auto; border-style: solid; border-width: 1px; border-color: gray"
                onMouseLeave (fun _ -> message TreeView.Message.Unhover)
            ]

        require dependencies (
            model.tree |> VirtualTree.view (TreeView.Message.Virtual >> message) itemNode attributes
        )