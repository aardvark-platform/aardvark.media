namespace Aardvark.UI.Primitives.ColorPicker2

open Aardvark.UI
open Aardvark.Base
open FSharp.Data.Adaptive

// https://github.com/aardvark-community/spectrum

// TODO: Move to Aardvark.UI.Primitives namespace and delete old color picker
module ColorPicker =

    [<AutoOpen>]
    module private Internals =

        let dependencies =
            [
                { kind = Stylesheet; name = "spectrumStyle"; url = "resources/spectrum.css" }
                { kind = Stylesheet; name = "spectrumOverridesStyle"; url = "resources/spectrum-overrides.css" }
                { kind = Script; name = "spectrum"; url = "resources/spectrum.js" }
            ]

    [<Struct>]
    type Palette = { colors : C4b[][] }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Palette =

        let inline isEmpty (palette : Palette) =
            palette.colors |> Array.sumBy Array.length = 0

        let inline ofArray (maxPerRow : int) (colors : ^Color[]) : Palette =
            let rows = (colors.Length + maxPerRow - 1) / maxPerRow

            let colors =
                Array.init rows (fun r ->
                    let cols = min maxPerRow (colors.Length - r * maxPerRow)
                    Array.init cols (fun c -> c4b colors.[r * maxPerRow + c])
                )

            { colors = colors }

        let inline map (mapping : C4b -> 'T) (palette : Palette) =
            palette.colors |> Array.map (Array.map mapping)

        let inline maxSize (palette : Palette) =
            if palette.colors.Length = 0 then 0
            else palette.colors |> Array.map Array.length |> Array.max

        /// Empty palette.
        let Empty =
            { colors = Array.empty }

        /// Palette of 64 colors.
        // Generated with https://supercolorpalette.com
        let Default =
            ofArray 8 [|
                C3b(000uy, 000uy, 000uy); C3b(068uy, 068uy, 068uy); C3b(102uy, 102uy, 102uy); C3b(153uy, 153uy, 153uy)
                C3b(204uy, 204uy, 204uy); C3b(238uy, 238uy, 238uy); C3b(243uy, 243uy, 243uy); C3b(255uy, 255uy, 255uy)
                C3b(255uy, 000uy, 000uy); C3b(255uy, 153uy, 000uy); C3b(255uy, 255uy, 000uy); C3b(000uy, 255uy, 000uy)
                C3b(000uy, 255uy, 255uy); C3b(000uy, 000uy, 255uy); C3b(153uy, 000uy, 255uy); C3b(255uy, 000uy, 255uy)
                C3b(244uy, 047uy, 047uy); C3b(244uy, 165uy, 047uy); C3b(244uy, 244uy, 047uy); C3b(047uy, 244uy, 047uy)
                C3b(047uy, 244uy, 244uy); C3b(047uy, 047uy, 244uy); C3b(165uy, 047uy, 244uy); C3b(244uy, 047uy, 244uy)
                C3b(237uy, 090uy, 090uy); C3b(237uy, 178uy, 090uy); C3b(237uy, 237uy, 090uy); C3b(090uy, 237uy, 090uy)
                C3b(090uy, 237uy, 237uy); C3b(090uy, 090uy, 237uy); C3b(178uy, 090uy, 237uy); C3b(237uy, 090uy, 237uy)
                C3b(233uy, 129uy, 129uy); C3b(233uy, 191uy, 129uy); C3b(233uy, 233uy, 129uy); C3b(129uy, 233uy, 129uy)
                C3b(129uy, 233uy, 233uy); C3b(129uy, 129uy, 233uy); C3b(191uy, 129uy, 233uy); C3b(233uy, 129uy, 233uy)
                C3b(233uy, 165uy, 165uy); C3b(233uy, 206uy, 165uy); C3b(233uy, 233uy, 165uy); C3b(165uy, 233uy, 165uy)
                C3b(165uy, 233uy, 233uy); C3b(165uy, 165uy, 233uy); C3b(206uy, 165uy, 233uy); C3b(233uy, 165uy, 233uy)
                C3b(236uy, 198uy, 198uy); C3b(236uy, 221uy, 198uy); C3b(236uy, 236uy, 198uy); C3b(198uy, 236uy, 198uy)
                C3b(198uy, 236uy, 236uy); C3b(198uy, 198uy, 236uy); C3b(221uy, 198uy, 236uy); C3b(236uy, 198uy, 236uy)
                C3b(243uy, 226uy, 226uy); C3b(243uy, 236uy, 226uy); C3b(243uy, 243uy, 226uy); C3b(226uy, 243uy, 226uy)
                C3b(226uy, 243uy, 243uy); C3b(226uy, 226uy, 243uy); C3b(236uy, 226uy, 243uy); C3b(243uy, 226uy, 243uy)
            |]

        /// Palette of 32 colors.
        let Reduced =
            ofArray 8 [|
                C3b(000uy, 000uy, 000uy); C3b(068uy, 068uy, 068uy); C3b(102uy, 102uy, 102uy); C3b(153uy, 153uy, 153uy)
                C3b(204uy, 204uy, 204uy); C3b(238uy, 238uy, 238uy); C3b(243uy, 243uy, 243uy); C3b(255uy, 255uy, 255uy)
                C3b(255uy, 000uy, 000uy); C3b(255uy, 153uy, 000uy); C3b(255uy, 255uy, 000uy); C3b(000uy, 255uy, 000uy)
                C3b(000uy, 255uy, 255uy); C3b(000uy, 000uy, 255uy); C3b(153uy, 000uy, 255uy); C3b(255uy, 000uy, 255uy)
                C3b(228uy, 063uy, 063uy); C3b(228uy, 162uy, 063uy); C3b(228uy, 228uy, 063uy); C3b(063uy, 228uy, 063uy)
                C3b(063uy, 228uy, 228uy); C3b(063uy, 063uy, 228uy); C3b(162uy, 063uy, 228uy); C3b(228uy, 063uy, 228uy)
                C3b(209uy, 117uy, 117uy); C3b(209uy, 172uy, 117uy); C3b(209uy, 209uy, 117uy); C3b(117uy, 209uy, 117uy)
                C3b(117uy, 209uy, 209uy); C3b(117uy, 117uy, 209uy); C3b(172uy, 117uy, 209uy); C3b(209uy, 117uy, 209uy)
            |]

        /// Palette of 10 colors.
        let Basic =
            ofArray 5 [|
                C3b(000uy, 000uy, 000uy); C3b(255uy, 255uy, 255uy); C3b(255uy, 000uy, 000uy); C3b(255uy, 153uy, 000uy); C3b(255uy, 255uy, 000uy)
                C3b(000uy, 255uy, 000uy); C3b(000uy, 255uy, 255uy); C3b(000uy, 000uy, 255uy); C3b(153uy, 000uy, 255uy); C3b(255uy, 000uy, 255uy)
            |]


    type Format =
        | None = 0
        | Hex = 1
        | Hex3 = 2
        | HSL = 3
        | RGB = 4
        | Name = 5

    [<RequireQualifiedAccess>]
    type DisplayMode =
        /// Disabled dropdown menu.
        | Disabled = 0

        /// Show a dropdown menu, which can be disabled.
        | Dropdown = 1

        /// Always show the full control as an inline-block.
        | Inline = 2

    type TextInput =
        /// Do not show the input field.
        | None = 0

        /// Show the input field but disable it.
        | Disabled = 1

        /// Allow text input via the input field.
        | Enabled = 2

    type PickerStyle =
        {
            /// Show an alpha slider.
            showAlpha : bool

            /// Show 'Choose' and 'Cancel' buttons below the color picker.
            showButtons : bool

            /// If true the picker can be toggled and is hidden initially.
            toggle : bool

            /// Determines whether text input is allowed.
            textInput : TextInput

            /// Text label for the choose button (if showButtons = true).
            labelChoose : string

            /// Text label for the cancel button (if showButtons = true).
            labelCancel : string

            /// Text label for the toggle more button (if toggle = true).
            labelToggleMore : string

            /// Text label for the toggle less button (if toggle = true).
            labelToggleLess : string
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickerStyle =

        /// Default picker that is always visible.
        let Default =
            { showAlpha       = false
              showButtons     = false
              toggle          = false
              textInput       = TextInput.Disabled
              labelChoose     = "Choose"
              labelCancel     = "Cancel"
              labelToggleMore = "More"
              labelToggleLess = "Less" }

        /// Default picker that can be toggled.
        let Toggle =
            { Default with toggle = true }

        /// Default picker with an alpha slider.
        let DefaultWithAlpha =
            { Default with showAlpha = true }

        /// Default picker that can be toggled and has an alpha slider.
        let ToggleWithAlpha =
            { Toggle with showAlpha = true }

    /// Maximum selection size depends on the chosen palette (equal to length of longest row).
    [<Literal>]
    let AutoSelectionSize = -1

    type Config =
        {
            /// The palette to choose colors from.
            /// Disabled if None.
            palette : Palette option

            /// Maximum number of recently chosen colors (zero for disabling the selection palette).
            /// Only visible if regular palette is enabled. Automatically chosen based on palette if equal to AutoSelectionSize.
            maxSelectionSize : int

            /// Local storage key used to persist the selection palette.
            localStorageKey : string option

            /// Display mode of the control.
            displayMode : DisplayMode

            /// Determines how the color picker looks and behaves.
            /// If None only the palette is displayed.
            pickerStyle : PickerStyle option

            /// The preferred display format of the colors.
            preferredFormat : Format

            /// Automatically hide after a color from the palette is selected.
            hideAfterPaletteSelect : bool

            /// Use a dark color theme for the UI.
            darkTheme : bool
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Config =

        /// Picker with a palette of 64 colors.
        let Default =
            { palette                = Some Palette.Default
              maxSelectionSize       = AutoSelectionSize
              localStorageKey        = None
              displayMode            = DisplayMode.Dropdown
              pickerStyle            = Some PickerStyle.Default
              preferredFormat        = Format.Hex
              hideAfterPaletteSelect = false
              darkTheme              = false }

        /// Disabled picker dropdown menu.
        let Disabled =
            { Default with displayMode = DisplayMode.Disabled }

        /// Picker with a palette of 64 colors and an alpha slider.
        let DefaultWithAlpha =
            { Default with pickerStyle = Some PickerStyle.DefaultWithAlpha }

        /// Only shows the palette, the picker is hidden.
        let PaletteOnly =
            { Default with pickerStyle = None }

        /// Only shows the picker, the palette is hidden.
        let PickerOnly =
            { Default with palette = None }

        /// Only shows the picker (with an alpha slider), the palette is hidden.
        let PickerOnlyWithAlpha =
            { DefaultWithAlpha with palette = None }

        /// Shows both the palette and a picker, which can be toggled.
        let Toggle =
            { Default with pickerStyle = Some PickerStyle.Toggle }

        /// Shows both the palette and a picker, which can be toggled and has an alpha slider.
        let ToggleWithAlpha =
            { Default with pickerStyle = Some PickerStyle.ToggleWithAlpha }

        /// Contains predefined configurations with a dark theme.
        module Dark =

            /// Picker with a palette of 64 colors.
            let Default =
                { Default with darkTheme = true }

            /// Disabled picker dropdown menu.
            let Disabled =
                { Disabled with darkTheme = true }

            /// Picker with a palette of 64 colors and an alpha slider.
            let DefaultWithAlpha =
                { DefaultWithAlpha with darkTheme = true }

            /// Only shows the palette, the picker is hidden.
            let PaletteOnly =
                { PaletteOnly with darkTheme = true }

            /// Only shows the picker, the palette is hidden.
            let PickerOnly =
                { PickerOnly with darkTheme = true }

            /// Only shows the picker (with an alpha slider), the palette is hidden.
            let PickerOnlyWithAlpha =
                { PickerOnlyWithAlpha with darkTheme = true }

            /// Shows both the palette and a picker, which can be toggled.
            let Toggle =
                { Toggle with darkTheme = true }

            /// Shows both the palette and a picker, which can be toggled and has an alpha slider.
            let ToggleWithAlpha =
                { ToggleWithAlpha with darkTheme = true }


    let view (config : Config) (message : C4b -> 'msg) (color : aval<C4b>) =
        let boot =
            let palette =
                match config.palette with
                | None -> "showPalette: false"
                | Some p ->
                    let values = p |> Palette.map Html.color |> Pickler.jsonToString
                    $"showPalette: true, palette: {values}"

            let selectionPalette =
                let size =
                    if config.maxSelectionSize = AutoSelectionSize then
                        match config.palette with
                        | Some p -> Palette.maxSize p
                        | _ -> 0
                    else
                        config.maxSelectionSize

                if size = 0 then
                    "showSelectionPalette: false"
                else
                    let ls = match config.localStorageKey with Some k -> $", localStorageKey: '{k}'" | _ -> ""
                    $"showSelectionPalette: true, maxSelectionSize: {size}{ls}"

            let showAlpha =
                match config.pickerStyle with
                | Some { showAlpha = true } -> "showAlpha: true"
                | _ -> "showAlpha: false"

            let paletteOnly =
                match config.pickerStyle with
                | None -> "showPaletteOnly: true"
                | Some { toggle = false } -> "showPaletteOnly: false"
                | Some p ->
                    "showPaletteOnly: true, togglePaletteOnly: true, " +
                    $"togglePaletteMoreText: '{p.labelToggleMore}', " +
                    $"togglePaletteLessText: '{p.labelToggleLess}'"

            let textInput =
                match config.pickerStyle with
                | Some { textInput = TextInput.None } -> "showInput: false"
                | _ -> "showInput: true"

            let showButtons =
                match config.pickerStyle with
                | Some p when p.showButtons -> $"showButtons: true, chooseText: '{p.labelChoose}', cancelText: '{p.labelCancel}'"
                | _ -> "showButtons: false"

            let format =
                match config.preferredFormat with
                | Format.Hex -> "preferredFormat: 'hex'"
                | Format.Hex3 -> "preferredFormat: 'hex3'"
                | Format.HSL -> "preferredFormat: 'hsl'"
                | Format.RGB -> "preferredFormat: 'rgb'"
                | Format.Name -> "preferredFormat: 'name'"
                | _ -> ""

            let flag value =
                if value then "true" else "false"

            String.concat "" [
                "const $self = $('#__ID__');"

                "$self.spectrum({"
                $"    appendTo: 'replacer',"
                $"    clickoutFiresChange: false,"
                $"    fireChangeImmediately: true,"

                if config.darkTheme then
                    "    replacerClassName: 'dark',"
                    "    containerClassName: 'dark',"

                $"    {palette},"
                $"    {selectionPalette},"
                $"    {showAlpha},"
                $"    {paletteOnly},"
                $"    {textInput},"
                $"    {showButtons},"
                $"    flat: {flag (config.displayMode = DisplayMode.Inline)},"
                $"    hideAfterPaletteSelect: {flag config.hideAfterPaletteSelect},"
                $"    disabled: {flag (config.displayMode = DisplayMode.Disabled)},"
                $"    {format}"
                "});"

                "$self.on('change.spectrum', function (e, tinycolor) {"
                "    aardvark.processEvent('__ID__', 'data-event', tinycolor.toHex8());"
                "});"

                match config.pickerStyle with
                | Some p ->
                    // Disable text input if requested
                    if p.textInput = TextInput.Disabled then
                        "$self.spectrum('container').find('.sp-input').attr('disabled', true);"
                | _ ->
                    ()

                "valueCh.onmessage = function(rgba) {"
                "    $self.spectrum('set', rgba);"
                "};"
            ]

        let rgba = color |> AVal.map Html.color

        let attributes =
            AttributeMap.ofList [
                onEvent "data-event" [] (fun data ->
                    let argb = Pickler.unpickleOfJson<string> <| List.head data
                    message <| Col.ParseHex(argb.Substring(2) + argb.Substring(0, 2)) // Spectrum returns ARGB...
                )
            ]

        require dependencies (
            onBoot' ["valueCh", AVal.channel rgba] boot (
                Incremental.input attributes
            )
        )