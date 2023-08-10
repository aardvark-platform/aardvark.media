namespace TreeView.App

open Aardvark.UI
open FSharp.Data.Adaptive
open System

open TreeView.Model
open VirtualTree.Model
open VirtualTree.App
open VirtualTree.Utilities

module TreeView =

    // TODO: Remove when updated to >= Aardvark.Base 5.2.26
    module private ArraySegment =

        let inline contains (value : 'T) (segment : ArraySegment<'T>) =
            let mutable state = false
            let mutable i = 0

            while not state && i < segment.Count do
                state <- value = segment.[i]
                i <- i + 1

            state

        let inline reduce (reduction : 'T -> 'T -> 'T) (segment : ArraySegment<'T>)=
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt (reduction)
            let mutable res = segment.[0]

            for i = 1 to segment.Count - 1 do
                res <- f.Invoke(res, segment.[i])

            res

    [<AutoOpen>]
    module private Events =

        let disableClickPropagation =
            sprintf "$('#__ID__').on('click', function(e) { e.stopPropagation(); } ); "

        let onClickModifiers (cb : KeyModifiers -> 'msg) =
            onEvent "onclick" ["{ shift: event.shiftKey, alt: event.altKey, ctrl: event.ctrlKey  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)


    let update (message : TreeView.Message<'Key>) (model : TreeView<'Key, 'Value>) =
        match message with
        | TreeView.Message.Toggle key ->
            match model.tree.hierarchy |> FlatTree.tryIndexOf key with
            | ValueSome index ->
                let count = model.tree.hierarchy |> FlatTree.descendantCount key
                let buffer = Array.copy model.visibility

                // Set state for self and descendants
                let state =
                    if buffer.[index] = Visibility.Visible then
                        Visibility.Hidden
                    else
                        Visibility.Visible

                for i = index to index + count - 1 do
                    buffer.[i] <- state

                // Check and adjust ancestors based on their descendants
                let path = model.tree.hierarchy |> FlatTree.rootPath key

                for i = path.Length - 2 downto 0 do
                    let curr = path.[i]
                    let index = model.tree.hierarchy |> FlatTree.indexOf curr
                    let count = model.tree.hierarchy |> FlatTree.descendantCount curr

                    if count > 1 then
                        buffer.[index] <- ArraySegment(buffer, index + 1, count - 1) |> ArraySegment.reduce (|||)

                { model with visibility = buffer}

            | _ ->
                model

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

            let checkbox =
                let attributes =
                    AttributeMap.ofAMap <| amap {
                        let! visibility = model.visibility
                        let! hierarchy = model.tree.hierarchy

                        let index =
                            hierarchy |> FlatTree.indexOf item.Value

                        let icon =
                            match visibility.[index] with
                            | Visibility.Hidden  -> "square"
                            | Visibility.Visible -> "check square outline"
                            | _                  -> "minus square outline"

                        yield clazz $"{icon} inverted link icon"
                        yield onClick (fun _ -> message <| TreeView.Message.Toggle item.Value)
                    }

                onBoot disableClickPropagation (
                    Incremental.i attributes AList.empty
                )

            let spacer =
                div [style $"width: {indent}px"] []

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

                    onBoot disableClickPropagation (
                        i [ clazz $"{icon} link inverted icon"; onClick (fun _ -> collapseMessage) ] []
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
                    yield style $"display: flex; justify-content: flex-start; align-items: center; padding: 5px"
                    yield onMouseEnter (fun _ -> message <| TreeView.Message.Hover item.Value)
                    yield onMouseLeave (fun _ -> message TreeView.Message.Unhover)
                    yield onClickModifiers (fun modifiers -> message <| TreeView.Message.Click (item.Value, modifiers))
                }

            Incremental.div attributes <| alist {
                let! v = value
                yield spacer
                yield collapseIcon
                yield checkbox
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