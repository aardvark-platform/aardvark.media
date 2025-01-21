module VirtualTreeExample.App

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive

open VirtualTreeExample.Model
open TreeView.Model
open TreeView.App

let private rnd = RandomSystem(0)

let private getIcon =
    let icons =
        [| "recycle"
           "book"
           "sync alternate"
           "film"
           "save outline" |]

    fun () -> icons.[rnd.UniformInt icons.Length]

let private getColor =
    let colors =
        [| C3b.AliceBlue
           C3b.CornflowerBlue
           C3b.BlueViolet
           C3b.Crimson
           C3b.DarkSlateGray
           C3b.Fuchsia |]

    fun () -> colors.[rnd.UniformInt colors.Length]

let update (model : Model) (message : Message) =
    match message with
    | SetCount count ->
        { model with count = count}

    | Generate ->
        let hierarchy = Array.zeroCreate<int * Item * ResizeArray<int>> (model.count + 1)

        let root =
            { icon = getIcon()
              label = "Root"
              color = getColor() }

        let roots = ResizeArray()

        hierarchy.[0] <- (-1, root, roots)

        for i = 1 to model.count do
            let item =
                { icon = getIcon()
                  label = $"Item #{i}"
                  color = getColor() }

            let level, children =
                if rnd.UniformDouble() < 0.3 then 0, roots
                else
                    let parent = rnd.UniformInt i
                    let level, _, children = hierarchy.[parent]

                    if level > 10 then 0, roots
                    else level + 1, children

            children.Add i
            hierarchy.[i] <- (level, item, ResizeArray())

        let getChildren (id : int) =
            let _, _, c = hierarchy.[id]
            c

        let values =
            hierarchy
            |> Array.mapi (fun i (_, item, _) -> i, item)
            |> HashMap.ofArray

        { model with treeView = model.treeView |> TreeView.set getChildren values 0 }

    | Scroll ->
        let target = rnd.UniformInt model.treeView.values.Count
        let msg = TreeView.Message.ScrollTo target
        Log.line "Scroll to #%d" target
        { model with treeView = model.treeView |> TreeView.update msg }

    | TreeViewAction msg ->
        { model with treeView = model.treeView |> TreeView.update msg }

let view (model : AdaptiveModel) =
    let item (key : int) (item : AdaptiveItem) =
        let iconAttributes =
            AttributeMap.ofAMap <| amap {
                let! icon = item.icon
                yield clazz $"icon {icon}"
            }

        div [style "color: white"] [
            Incremental.i iconAttributes AList.empty
            Incremental.text item.label
        ]

    require Html.semui (
        body [ style "display: flex; flex-flow: column; overflow-y: hidden; background: #1B1C1E"] [
            div [style "flex: 0 1 auto"] [
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
                    style "margin: 5px"
                    onClick (fun _ -> Generate)
                ] [
                    text "Generate"
                ]

                button [
                    clazz "ui button"
                    style "margin: 5px"
                    onClick (fun _ -> TreeViewAction <| TreeView.Message.CollapseAll)
                ] [
                    text "Collapse"
                ]

                button [
                    clazz "ui button"
                    style "margin: 5px"
                    onClick (fun _ -> TreeViewAction <| TreeView.Message.UncollapseAll)
                ] [
                    text "Uncollapse"
                ]

                button [
                    clazz "ui button"
                    style "margin: 5px"
                    onClick (fun _ -> Scroll)
                ] [
                    text "Scroll"
                ]

                simplecheckbox {
                    attributes [clazz "ui inverted checkbox"; style "margin-left: 10px"]
                    state model.treeView.tree.showRoot
                    toggle (TreeViewAction TreeView.Message.ToggleRoot)
                    content [ text "Show root" ]
                }
            ]

            let atts = AttributeMap.ofList [style "flex: 1 1 auto"]
            model.treeView |> TreeView.view atts TreeViewAction item
        ]
    )


let threads (model : Model) =
    ThreadPool.empty

let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
                count = 100
                treeView = TreeView.empty
            }
        update = update
        view = view
    }
