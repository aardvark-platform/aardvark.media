namespace Aardvark.UI.Tests

open System
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``ColorPicker Tests`` =

    let private values = [
        "apostrophe", "O'Reilly"
        "backslash", @"C:\tmp\new"
        "line breaks", "first\nsecond\r\nthird"
        "double quotes", "\"quoted\""
        "Unicode", "Grüße λ 漢字 😀"
        "empty", ""
        "null", null
        "plain", "unchanged"
    ]

    let private fields = ["chooseText"; "cancelText"; "togglePaletteMoreText"; "togglePaletteLessText"; "localStorageKey"]
    let private enabled =
        { ColorPicker.Config.Toggle with
            pickerStyle = Some { ColorPicker.PickerStyle.Toggle with showButtons = true }
            maxSelectionSize = 4; localStorageKey = Some "ordinary" }

    let private configure field value =
        let style = enabled.pickerStyle.Value
        match field with
        | "chooseText" -> { enabled with pickerStyle = Some { style with labelChoose = value } }
        | "cancelText" -> { enabled with pickerStyle = Some { style with labelCancel = value } }
        | "togglePaletteMoreText" -> { enabled with pickerStyle = Some { style with labelToggleMore = value } }
        | "togglePaletteLessText" -> { enabled with pickerStyle = Some { style with labelToggleLess = value } }
        | _ -> { enabled with localStorageKey = Some value }

    let private view config = ColorPicker.view config id (AVal.constant (C4b(17uy, 34uy, 51uy, 68uy)))
    let private boot config =
        match (view config).Boot with
        | ValueSome generate -> generate "picker"
        | _ -> failtest "ColorPicker has no boot code"

    let private literal field code =
        let pattern = @"(?:^|[,{])\s*" + Regex.Escape(field) + @":\s*'((?:\\[^\r\n]|[^'\\\r\n])*)'(?=\s*[,}])"
        let matched = Regex.Match(code, pattern)
        Expect.isTrue matched.Success $"{field}: expected one complete single-quoted string literal"
        // Decode the JSON-compatible escapes independently, allowing unescaped double quotes
        // in the single-quoted source. Browser semantics are verified separately in Chromium.
        let json = StringBuilder("\"")
        let mutable escaped = false
        for character in matched.Groups[1].Value do
            if character = '"' && not escaped then json.Append("\\\"") |> ignore
            else json.Append character |> ignore
            if escaped then escaped <- false
            elif character = '\\' then escaped <- true
        json.Append('"') |> ignore
        JsonSerializer.Deserialize<string>(json.ToString())

    let private check field name value =
        let actual = boot (configure field value) |> literal field
        Expect.equal actual (if isNull value then "" else value) $"{field}, {name}: exact configured text"

    let private defaults () =
        let code = boot enabled
        for field, expected in ["chooseText", "Choose"; "cancelText", "Cancel"; "togglePaletteMoreText", "More"; "togglePaletteLessText", "Less"; "localStorageKey", "ordinary"] do
            Expect.equal (literal field code) expected $"{field}: default text"
        let standard = boot ColorPicker.Config.Default
        for text in ["showPalette: true"; "showSelectionPalette: true, maxSelectionSize: 8"; "showButtons: false"; "showPaletteOnly: false"; "flat: false"; "disabled: false"] do
            Expect.stringContains standard text "Default enablement is unchanged"
        for field in fields do Expect.isFalse (standard.Contains(field + ":")) $"{field}: disabled or absent by default"

    let private disabled () =
        let unsafeText = "not'used\\\n"
        let style =
            { ColorPicker.PickerStyle.Default with
                labelChoose = unsafeText; labelCancel = unsafeText
                labelToggleMore = unsafeText; labelToggleLess = unsafeText }
        let config = { ColorPicker.Config.Default with pickerStyle = Some style; maxSelectionSize = 0; localStorageKey = Some unsafeText }
        let code = boot config
        Expect.equal code (boot { config with pickerStyle = Some ColorPicker.PickerStyle.Default; localStorageKey = None })
            "Disabled options never interpolate their values"
        for field in fields do Expect.isFalse (code.Contains(field + ":")) $"{field}: omitted when disabled"
        let paletteOnly = boot ColorPicker.Config.PaletteOnly
        Expect.stringContains paletteOnly "showPaletteOnly: true" "Palette-only behavior"
        for field in fields do Expect.isFalse (paletteOnly.Contains(field + ":")) $"{field}: palette-only omission"

    let private bindings () =
        let node = view ColorPicker.Config.Default
        Expect.equal (node.Required |> List.map (fun reference -> reference.url))
            ["resources/spectrum.css"; "resources/spectrum-overrides.css"; "resources/spectrum.js"] "Bundled dependencies"
        Expect.equal (node.Channels |> Map.toList |> List.map fst) ["valueCh"] "Color channel"
        let input = node :?> VoidNode<C4b>
        match input.Attributes.Content |> AVal.force |> HashMap.find "data-event" with
        | AttributeValue.Event _ -> ()
        | _ -> failtest "Color event is missing"
        Expect.stringContains (boot ColorPicker.Config.Default) "valueCh.onmessage = function(rgba)" "Channel boot binding"

    [<Tests>]
    let tests =
        testList "ColorPicker Tests" [
            test "Labels retain their exact text" {
                for field in fields |> List.take 4 do
                    for name, value in values do check field name value
            }
            test "Local-storage keys retain their exact text" {
                for name, value in values do check "localStorageKey" name value
            }
            testCase "Defaults and disabled options are unchanged" (fun () -> defaults (); disabled ())
            testCase "Dependencies and bindings are unchanged" bindings
        ]
