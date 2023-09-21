module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Input

let initial =
    {
        active = true
        value = Constant.Pi
        intValue = 13
        decValue = 1.1m
        uintValue = 1u
        name = "Pi"
        alt = Some A
        options = HashMap.ofList [A, "A"; B, "B"; C, "C";  D, "D"]
        enumValue = EnumValue.Value1
    }


let rand = System.Random()

let update (model : Model) (msg : Message) =
    match msg with
    | ToggleActive ->
        { model with active = not model.active }

    | SetValue v ->
        { model with value = v }

    | SetInt v ->
        { model with intValue = v }

    | SetDecimal v ->
        { model with decValue = v }

    | SetUInt v ->
        { model with uintValue = v }

    | SetName n ->
        { model with name = n; options = HashMap.add (Custom n) n model.options }

    | SetAlternative a ->
        { model with alt = a }

    | SetEnumValue v ->
        { model with enumValue = v }

    | Reset ->
        initial

let view (model : AdaptiveModel) =
    let alternatives = model.options |> AMap.map (fun _ v -> text v)

    let enumValues : amap<EnumValue, DomNode<Message>> =
        Enum.GetValues<EnumValue>()
        |> Array.map (fun e -> e, text (string e))
        |> AMap.ofArray

    let description (str : string) =
        div [ style "margin-bottom: 10px" ] [ text str ]

    body [style "background-color: lightslategrey"] [
        div [ clazz "ui vertical inverted menu"; style "min-width: 250px" ] [

            div [ clazz "item" ] [
                button [clazz "ui inverted labeled icon button"; onClick (fun _ -> Reset)] [
                    i [clazz "red delete icon"] []
                    text "Reset"
                ]
            ]

            div [ clazz "item" ] [
                simplecheckbox {
                    attributes [clazz "ui inverted checkbox"]
                    state model.active
                    toggle ToggleActive
                    content [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ]
                }
                //checkbox [clazz "ui inverted checkbox"] model.active ToggleActive [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ]
            ]

            div [ clazz "item" ] [
                checkbox [clazz "ui inverted toggle checkbox"] model.active ToggleActive "Is the thing active?"
            ]

            div [ clazz "item" ] [
                description "Numeric input (float)"
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.value
                    update SetValue
                    step 0.1
                    largeStep 1.0
                    min 1.0
                    max 100.0
                }
                //numeric { min = -1E15; max = 1E15; smallStep = 0.1; largeStep = 100.0 } [clazz "ui inverted input"] model.value SetValue
            ]

            div [ clazz "item" ] [
                description "Numeric input (integer)"
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.intValue
                    update SetInt
                    step 1
                    largeStep 5
                    min -100000
                    max 100000
                }
                //numeric { min = 0; max = 10000; smallStep = 1; largeStep = 10 } [clazz "ui inverted input"] model.intValue SetInt
            ]

            div [ clazz "item" ] [
                description "Numeric input (decimal)"
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.decValue
                    update SetDecimal
                    step 1m
                    largeStep 5m
                    min -100000m
                    max 100000m
                }
            ]

            div [ clazz "item" ] [
                description "Numeric input (unsigned integer)"
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.uintValue
                    update SetUInt
                    step 1u
                    largeStep 5u
                    min 0u
                    max 100000u
                }
            ]

            div [ clazz "item" ] [
                description "Slider (float)"
                slider { min = 1.0; max = 100.0; step = 0.1 } [clazz "ui inverted red slider"] model.value SetValue
            ]

            div [ clazz "item" ] [
                description "Slider (integer)"
                slider { min = 0; max = 20; step = 1 } [clazz "ui inverted blue slider"] model.intValue SetInt
            ]

            div [ clazz "item" ] [
                description "Text input"
                textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] model.name SetName
            ]

            div [ clazz "item" ] [
                description "Dropdown (non-clearable)"
                dropdownUnclearable [ clazz "inverted selection" ] enumValues model.enumValue SetEnumValue
            ]

            div [ clazz "item" ] [
                description "Dropdown (clearable)"
                dropdown { mode = DropdownMode.Text <| Some "blub"; onTrigger = TriggerDropdown.Hover } [ clazz "inverted selection" ] alternatives model.alt SetAlternative
            ]

            div [ clazz "item" ] [
                description "Dropdown (icon mode)"
                dropdown { mode = DropdownMode.Icon "sidebar"; onTrigger = TriggerDropdown.Hover } [ clazz "inverted icon top left pointing dropdown circular button" ] alternatives model.alt SetAlternative
            ]
        ]
    ]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.empty
        initial = initial
        update = update
        view = view
    }