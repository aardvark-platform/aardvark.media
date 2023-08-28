module VirtualListExample.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open VirtualListExample.Model

module VirtualList =

    // Channel that triggers whenever the adaptive value changes (ignoring the actual value)
    type private SignalChannelReader<'T>(value : aval<'T>) =
        inherit ChannelReader()

        override x.Release() =()
        override x.ComputeMessages t =
            value.GetValue t |> ignore
            [ "0" ]

    type private SignalChannel<'T>(value : aval<'T>) =
        inherit Channel()
        override x.GetReader() = new SignalChannelReader<_>(value) :> ChannelReader


    let update (message : VirtualList.Message) (list : VirtualList<'T>) =
        match message with
        | VirtualList.Message.Resize height ->
            let height = { height with itemHeight = max height.itemHeight list.height.itemHeight }
            Log.line "%A" height
            { list with height = height }

        | VirtualList.Message.Scroll offset ->
            Log.line "Scroll %A" offset
            { list with scrollOffset = offset }

    let view (message : VirtualList.Message -> 'msg) (itemNode : 'T -> DomNode<'msg>) (list : AdaptiveVirtualList<'T, _, _>) : DomNode<'msg> =
        let onUpdate =
            onEvent "onupdate" [ ] (
                List.head >> Pickler.unpickleOfJson >> VirtualList.Message.Resize >> message
            )

        let onScroll =
            onEvent "onscroll" [ "event.target.scrollTop" ] (
                List.head >> Pickler.unpickleOfJson >> VirtualList.Message.Scroll >> message
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

        let item (element : 'T) =
            div [clazz "item"; style "padding: 5px; color: white"] [itemNode element]

        let virtualItem (height : int) =
            div [clazz "item virtual"; style $"height: {height}px; visibility: hidden"] []

        // Number of elements the window extends beyond the actual visible space
        let border = 5

        let elements =
            alist {
                let! itemHeight = list.height.itemHeight

                if itemHeight = 0 then
                    let! elements = list.elements

                    // We do not know the item height yet, render two elements to determine it.
                    // Also we need the client height of the list, expand it to the max with a big virtual item.
                    if elements.Length > 0 then
                        yield virtualItem 9999

                        for i = 0 to (min elements.Length 2) - 1 do
                            yield item elements.[i]
                else
                    let! elements = list.elements
                    let! clientHeight = list.height.clientHeight
                    let! scrollOffset = list.scrollOffset

                    let first = ((scrollOffset / itemHeight) - border) |> clamp 0 (elements.Length - 1)
                    let last = (first + (clientHeight / itemHeight) + 2 * border) |> min (elements.Length - 1)

                    if first > 0 then
                        yield virtualItem (first * itemHeight)

                    for i = first to last do
                        yield item elements.[i]

                    if last < elements.Length - 1 then
                        yield virtualItem ((elements.Length - last - 1) * itemHeight)
            }

        let channels : (string * Channel) list = [
            "elementsCh", SignalChannel list.elements
            "scrollCh", AVal.channel list.scrollTarget
        ]

        onBoot' channels bootJs (
            elements |> Incremental.div (AttributeMap.ofList [
                clazz "ui inverted divided list"
                style "margin: 10px; height: 100%; max-height: 400px; overflow-y: auto; border-style: solid; border-width: 1px; border-color: gray"
                onUpdate; onScroll
            ])
        )

let private rnd = RandomSystem()

let update (model : Model) (msg : Message) =
    match msg with
    | SetCount count ->
        { model with count = count}

    | Generate ->
        let elements = Array.init model.count (fun i -> $"#{i}")
        { model with elements = model.elements |> VirtualList.set elements }

    | Scroll ->
        let target = model.elements |> VirtualList.length |> rnd.UniformInt
        { model with elements = model.elements |> VirtualList.scrollTo target }

    | VirtualListAction message ->
        { model with elements = model.elements |> VirtualList.update message }

let view (model : AdaptiveModel) =
    body [ style "background: #1B1C1E"] [
        require Html.semui (
            div [clazz "ui"; style "background: #1B1C1E; overflow-y: hidden"] [
                div [clazz "ui right labeled input"; style "margin: 10px"] [
                    simplenumeric {
                        attributes [clazz "ui inverted input"]
                        value model.count
                        update SetCount
                        min 1
                        max 10000000
                    }

                    div [clazz "ui basic label"] [
                        text "Elements"
                    ]
                ]

                button [
                    clazz "ui button"
                    style "margin: 10px"
                    onClick (fun _ -> Generate)
                ] [
                    text "Generate"
                ]

                button [
                    clazz "ui button"
                    style "margin: 10px"
                    onClick (fun _ -> Scroll)
                ] [
                    text "Scroll"
                ]

                model.elements |> VirtualList.view VirtualListAction text
            ]
        )
    ]


let threads (model : Model) =
    ThreadPool.empty

let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
                count = 100
                elements = VirtualList.empty
            }
        update = update
        view = view
    }