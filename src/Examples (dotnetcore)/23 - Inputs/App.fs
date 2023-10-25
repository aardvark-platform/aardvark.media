module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.ColorPicker2

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
        alts = IndexList.ofList [ A; D ]
        options = HashMap.ofList [A, "A"; B, "B"; C, "C";  D, "D"; Custom "Banana", "Banana"; Custom "Orange", "Orange"]
        enumValue = EnumValue.Value1
        color = C4b(C3b.Blue, 127uy)
    }

let update (model : Model) (msg : Message) =
    Log.warn "%A" msg

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

    | SetAlternatives a ->
        { model with alts = IndexList.ofList a }

    | SetEnumValue v ->
        { model with enumValue = v }

    | SetColor c ->
        { model with color = c }

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
        div [ clazz "ui vertical inverted menu"; style "min-width: 410px" ] [

            div [ clazz "item" ] [
                button [clazz "ui inverted labeled icon button"; onClick (fun _ -> Reset)] [
                    i [clazz "red delete icon"] []
                    text "Reset"
                ]
            ]

            // Checkboxes
            div [ clazz "header item" ] [
                h3 [] [ text "Checkboxes" ]
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

            // Sliders
            div [ clazz "header item" ] [
                h3 [] [ text "Sliders" ]
            ]

            div [ clazz "item" ] [
                description "Float"
                slider { min = 1.0; max = 100.0; step = 0.1 } [clazz "ui inverted red slider"] model.value SetValue
            ]

            div [ clazz "item" ] [
                description "Integer"
                slider { min = 0; max = 20; step = 1 } [clazz "ui inverted small bottom aligned labeled ticked blue slider"] model.intValue SetInt
            ]

            // Input fields
            div [ clazz "header item" ] [
                h3 [] [ text "Input fields" ]
            ]

            div [ clazz "item" ] [
                description "Numeric (float)"
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
                description "Numeric (integer)"
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
                description "Numeric (decimal)"
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
                description "Numeric (unsigned integer)"
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
                description "Text input with validation"
                textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] model.name SetName
            ]

            // Dropdowns
            div [ clazz "header item" ] [
                h3 [] [ text "Dropdown menus" ]
            ]

            div [ clazz "item" ] [
                description "Non-clearable"
                dropdownUnclearable [ clazz "inverted selection" ] enumValues model.enumValue SetEnumValue
            ]

            div [ clazz "item" ] [
                description "Clearable"
                dropdown { mode = DropdownMode.Text <| Some "blub"; onTrigger = TriggerDropdown.Hover } [ clazz "inverted selection" ] alternatives model.alt SetAlternative
            ]

            div [ clazz "item" ] [
                description "Icon mode"
                dropdown { mode = DropdownMode.Icon "sidebar"; onTrigger = TriggerDropdown.Hover } [ clazz "inverted icon top left pointing dropdown circular button" ] alternatives model.alt SetAlternative
            ]

            div [ clazz "item" ] [
                description "Multi select"
                let atts = AttributeMap.ofList [clazz "inverted clearable search"]
                dropdownMultiSelect atts None "Search..." alternatives model.alts SetAlternatives
            ]

            // Color picker
            div [ clazz "header item"; style "display: flex" ] [
                h3 [] [ text "Color picker" ]

                Incremental.div (AttributeMap.ofAMap <| amap {
                    let! c = model.color
                    yield style $"width: 16px; height: 16px; margin-left: 10px; margin-top: 5px; border: thin solid; background-color: #{c.ToHexString()}"
                }) AList.empty
            ]

            div [ clazz "item" ] [
                description "Dropdown variations"

                div [style "display: flex"] [
                    div [style "margin-right: 5px"] [
                        let cfg = { ColorPicker.Config.Dark with palette = Some ColorPicker.Palette.Basic }
                        ColorPicker.view cfg SetColor model.color
                    ]

                    div [style "margin-left: 5px; margin-right: 5px"] [
                        let cfg = { ColorPicker.Config.Dark with palette = Some ColorPicker.Palette.Reduced; pickerStyle = None }
                        ColorPicker.view cfg SetColor model.color
                    ]

                    div [style "margin-left: 5px; margin-right: 5px"] [
                        let cfg = { ColorPicker.Config.Dark with
                                        palette = None
                                        pickerStyle = Some ColorPicker.PickerStyle.Alpha
                                        preferredFormat = ColorPicker.Format.HSL }

                        ColorPicker.view cfg SetColor model.color
                    ]

                    div [style "margin-left: 5px"] [
                        let cfg = { ColorPicker.Config.Dark with displayMode = ColorPicker.DisplayMode.Disabled }
                        ColorPicker.view cfg SetColor model.color
                    ]
                ]
            ]

            div [ clazz "item" ] [
                description "Inline with persistent selection"

                let cfg = { ColorPicker.Config.Dark with
                                localStorageKey  = Some "aardvark.media.colorpicker.example"
                                palette          = Some ColorPicker.Palette.Default
                                pickerStyle      = Some { ColorPicker.PickerStyle.AlphaToggle with showButtons = true; textInput = ColorPicker.TextInput.Enabled }
                                displayMode      = ColorPicker.DisplayMode.Inline }

                ColorPicker.view cfg SetColor model.color
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