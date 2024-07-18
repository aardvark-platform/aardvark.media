module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

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

    let description (str : string) =
        div [ style "margin-bottom: 10px" ] [ text str ]

    body [style "background-color: lightslategrey"] [
        div [ clazz "ui vertical inverted menu"; style "min-width: 420px" ] [
            div [ clazz "item" ] [
                button [clazz "ui inverted labeled icon button"; onClick (fun _ -> Reset)] [
                    i [clazz "red delete icon"] []
                    text "Reset"
                ]
            ]

            accordionSimple' true [ clazz "inverted item" ] [
                // Checkboxes
                text "Checkboxes", div [ clazz "menu" ] [
                    div [ clazz "item" ] [
                        simplecheckbox {
                            attributes [clazz "inverted"]
                            state model.active
                            toggle ToggleActive
                            content [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ]
                        }
                        //checkbox [clazz "ui inverted checkbox"] model.active ToggleActive [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ]
                    ]

                    div [ clazz "item" ] [
                        checkbox [clazz "inverted toggle"] model.active ToggleActive "Is the thing active?"
                    ]
                ]

                // Sliders
                text "Sliders", div [ clazz "menu" ] [
                    div [ clazz "item" ] [
                        description "Float"
                        slider { min = 1.0; max = 100.0; step = 0.1 } [clazz "ui inverted red slider"] model.value SetValue
                    ]

                    div [ clazz "item" ] [
                        description "Integer"
                        slider { min = 0; max = 20; step = 1 } [clazz "ui inverted small bottom aligned labeled ticked blue slider"] model.intValue SetInt
                    ]
                ]

                // Input fields
                text "Input fields", div [ clazz "menu" ] [
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
                            iconRight "inverted users"
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
                            labelLeft "$"
                            labelRight "inverted basic" ".00"
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
                ]

                // Dropdowns
                text "Dropdown menus", div [ clazz "menu" ] [
                    div [ clazz "item" ] [
                        description "Non-clearable"
                        Dropdown.dropdownEnum SetEnumValue false None model.enumValue [ clazz "inverted" ] None
                    ]

                    div [ clazz "item" ] [
                        description "Clearable"
                        Dropdown.dropdownOption SetAlternative true None "Select..." model.alt [ clazz "inverted clearable" ] alternatives
                    ]

                    div [ clazz "item" ] [
                        description "Icon mode"
                        Dropdown.dropdownOption SetAlternative true (Some "sidebar") "" model.alt [ clazz "inverted icon top left pointing dropdown circular button" ] alternatives
                    ]

                    div [ clazz "item" ] [
                        description "Multi select"
                        let atts = AttributeMap.ofList [clazz "inverted clearable search"]
                        Dropdown.dropdownMultiSelect SetAlternatives false "Search..." model.alts atts alternatives
                    ]
                ]

                // Color picker
                let colorHeader =
                    span [] [
                        text "Color picker"

                        Incremental.i (AttributeMap.ofAMap <| amap {
                            let! c = model.color
                            yield style $"width: 16px; height: 16px; position: absolute; margin-left: 10px; border: thin solid; background-color: #{c.ToHexString()}"
                        }) AList.empty
                    ]

                colorHeader, div [ clazz "menu" ] [
                    div [ clazz "item" ] [
                        description "Dropdown variations"

                        div [style "display: flex"] [
                            div [style "margin-right: 5px"] [
                                let cfg = { ColorPicker.Config.Dark.Default with palette = Some ColorPicker.Palette.Basic }
                                ColorPicker.view cfg SetColor model.color
                            ]

                            div [style "margin-left: 5px; margin-right: 5px"] [
                                let cfg = { ColorPicker.Config.Dark.PaletteOnly with palette = Some ColorPicker.Palette.Reduced }
                                ColorPicker.view cfg SetColor model.color
                            ]

                            div [style "margin-left: 5px; margin-right: 5px"] [
                                let cfg = { ColorPicker.Config.Dark.PickerOnlyWithAlpha with preferredFormat = ColorPicker.Format.HSL }
                                ColorPicker.view cfg SetColor model.color
                            ]

                            div [style "margin-left: 5px"] [
                                ColorPicker.view ColorPicker.Config.Dark.Disabled SetColor model.color
                            ]
                        ]
                    ]

                    div [ clazz "item" ] [
                        description "Inline with persistent selection palette"

                        let cfg = { ColorPicker.Config.Dark.Default with
                                        localStorageKey  = Some "aardvark.media.colorpicker.example"
                                        pickerStyle      = Some { ColorPicker.PickerStyle.ToggleWithAlpha with showButtons = true; textInput = ColorPicker.TextInput.Enabled }
                                        displayMode      = ColorPicker.DisplayMode.Inline }

                        ColorPicker.view cfg SetColor model.color
                    ]
                ]
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