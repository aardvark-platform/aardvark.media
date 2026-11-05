namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.UI.Primitives.Golden
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Expecto

module ``GoldenLayout Json Tests`` =

    let private fixture = """
        {
          "root": {
            "type": "row", "sizeUnit": "fr", "size": 4,
            "content": [
              {
                "type": "stack", "sizeUnit": "%", "size": 70,
                "header": {"show": "top", "close": true, "popout": false, "maximise": true},
                "content": [
                  {
                    "type": "component", "componentType": "main", "title": "Main",
                    "isClosable": false, "sizeUnit": "%", "size": 37.75, "minSize": 17.5,
                    "header": {"show": "bottom", "close": false, "popout": false, "maximise": false},
                    "componentState": {"keepAlive": false}
                  },
                  {"type": "component", "componentType": "sibling", "title": "Sibling"}
                ]
              },
              {
                "type": "column", "sizeUnit": "fr", "size": 2,
                "content": [{"type": "component", "componentType": "inspector", "title": "Inspector"}]
              }
            ]
          },
          "openPopouts": [
            {
              "root": {"type": "component", "componentType": "detached", "title": "Detached"},
              "window": {"left": -120, "top": 30, "width": 800, "height": 600}
            },
            {
              "root": {"type": "stack", "content": [{"type": "component", "componentType": "other", "title": "Other"}]},
              "window": {"left": 100, "top": 200, "width": 320, "height": 240}
            }
          ]
        }
        """

    let private componentPath = "root.content[0].content[0]"
    let private windowPath = "openPopouts[0].window"

    let private componentFields =
        [
            componentPath, "isClosable", ["\"false\""; "0"]
            componentPath, "sizeUnit", ["false"; "42"]
            componentPath, "size", ["\"37.75\""; "false"]
            componentPath, "minSize", ["\"17.5\""; "false"]
            componentPath + ".header", "show", ["123"; "true"]
            componentPath + ".header", "close", ["\"false\""; "0"]
            componentPath + ".header", "popout", ["\"false\""; "0"]
            componentPath + ".header", "maximise", ["\"false\""; "0"]
            componentPath + ".componentState", "keepAlive", ["\"false\""; "0"]
        ]

    let private windowFields =
        [for name in ["left"; "top"; "width"; "height"] -> windowPath, name, ["\"800\""; "false"]]

    // Remove only the property, not its header, componentState or window container.
    let private change path name value (json: string) =
        let root = JObject.Parse json
        let parent = root.SelectToken(path) :?> JObject
        match value with
        | Some value -> parent.[name] <- JToken.Parse value
        | None -> Expect.isTrue (parent.Remove name) $"Fixture must contain {path}.{name}"
        root.ToString Formatting.None

    let private parse = GoldenLayout.Json.deserialize

    let private checkOmission path name value =
        let expected = fixture |> change path name None |> parse
        let actual = fixture |> change path name (Some value) |> parse
        Expect.equal actual expected $"{path}.{name} = {value} must use the omission fallback without changing surrounding layout"

    let private element id title : Element =
        { Id = id; Title = title; Closable = true; Header = None; Buttons = None
          MinSize = None; Size = Size.Weight 1; KeepAlive = true }

    let private mainElement =
        { element "main" "Main" with
            Closable = false; Header = Some Header.Bottom; Buttons = Some Buttons.None
            MinSize = Some 17; Size = Size.Percentage 37; KeepAlive = false }

    // Literal expected models are independent of both the parser and its serializer.
    let private expectedLayout main : WindowLayout =
        {
            Root = Some (Layout.RowOrColumn {
                IsRow = true; Size = Size.Weight 4
                Content = [
                    Layout.Stack {
                        Header = Header.Top; Buttons = Some (Buttons.Close ||| Buttons.Maximize)
                        Size = Size.Percentage 70; Content = [main; element "sibling" "Sibling"]
                    }
                    Layout.RowOrColumn {
                        IsRow = false; Size = Size.Weight 2
                        Content = [Layout.Element (element "inspector" "Inspector")]
                    }
                ]
            })
            PopoutWindows = [
                { Root = Layout.Element (element "detached" "Detached")
                  Position = Some (V2i(-120, 30)); Size = Some (V2i(800, 600)) }
                { Root = Layout.Stack {
                    Header = Header.Top; Buttons = None; Size = Size.Weight 1
                    Content = [element "other" "Other"]
                  }
                  Position = Some (V2i(100, 200)); Size = Some (V2i(320, 240)) }
            ]
        }

    // Literal JSON, not JObject-based fixture construction: the fixture must not parse dates first.
    let private stringLayouts : (string * string * (Element -> WindowLayout)) list = [
        "root", """{"root":{"type":"component","componentType":@ID@,"title":@TITLE@}}""",
            fun e -> { Root = Some(Layout.Element e); PopoutWindows = [] }
        "nested", """{"root":{"type":"row","content":[{"type":"column","content":[{"type":"stack","content":[{"type":"component","componentType":@ID@,"title":@TITLE@}]}]}]}}""",
            fun e -> {
                Root = Some(Layout.RowOrColumn {
                    IsRow = true; Size = Size.Weight 1; Content = [
                        Layout.RowOrColumn {
                            IsRow = false; Size = Size.Weight 1; Content = [
                                Layout.Stack { Header = Header.Top; Buttons = None; Size = Size.Weight 1; Content = [e] }
                            ]
                        }
                    ]
                })
                PopoutWindows = []
            }
        "popout", """{"openPopouts":[{"root":{"type":"component","componentType":@ID@,"title":@TITLE@},"window":{"left":-5,"top":17,"width":640,"height":480}}]}""",
            fun e -> { Root = None; PopoutWindows = [
                { Root = Layout.Element e; Position = Some(V2i(-5, 17)); Size = Some(V2i(640, 480)) }
            ] }
    ]

    let private withIdentity (idJson: string) (titleJson: string) (json: string) =
        json.Replace("@ID@", idJson).Replace("@TITLE@", titleJson)

    let private timestampStrings = [
        "2024-06-01T12:34:56Z"
        "2024-06-01T12:34:56+05:30"
        "2024-06-01T12:34:56-07:00"
        "2024-06-01T12:34:56.1234000Z"
        "2024-06-01T12:34:56.0000001+01:30"
        "2024-06-01T12:34:56"
    ]

    let private strings = [
        test "Date-shaped panel IDs and titles remain literal at every layout depth" {
            let failures = [
                for value in timestampStrings do
                    for name, json, expected in stringLayouts do
                        let token = "\"" + value + "\""
                        let actual = json |> withIdentity token token |> parse
                        let wanted = expected (element value value)
                        if actual <> wanted then yield $"{name}: expected exact ID/title {value} and unchanged layout defaults"
            ]
            Expect.isEmpty failures (String.concat "\n" failures)
        }
        test "Ordinary strings nulls and primitive identity conversions keep their meaning" {
            for idJson, titleJson, id, title in [
                "\"panel-1\"", "\"Ordinary title\"", "panel-1", "Ordinary title"
                "\"\"", "\"\"", "", ""
                "\"2024-06-01\"", "\"not a timestamp\"", "2024-06-01", "not a timestamp"
                "\"quote\\\" / \\u6771\\u4eac\"", "\"line\\nfeed &amp;\"", "quote\" / 東京", "line\nfeed &amp;"
                "null", "null", null, null
                "42", "12.5", "42", "12.5"
                "true", "false", "True", "False"
            ] do
                for name, json, expected in stringLayouts do
                    let actual = json |> withIdentity idJson titleJson |> parse
                    Expect.equal actual (expected (element id title)) $"{name}: preserve existing identity values and surrounding defaults"
        }
        test "Deserialization retains object and trailing-content validation" {
            let _, template, expected = stringLayouts.Head
            let json = template |> withIdentity "\"panel\"" "\"Panel\""
            let wanted = expected (element "panel" "Panel")
            for leading, trailing in ["", ""; " \r\n", " \t\r\n"; "/*leading*/", "/*trailing*/"; "//leading\n", "//trailing\n"] do
                Expect.equal (parse (leading + json + trailing)) wanted "Whitespace and comments remain accepted"
            Expect.equal (parse "{}") { Root = None; PopoutWindows = [] } "Empty object retains the empty-layout default"
            for malformed in [""; " "; "null"; "[]"; "true"; "42"; "\"text\""; "{"; "{\"root\":"; "{} /*unfinished"] do
                Expect.throwsT<JsonReaderException> (fun () -> parse malformed |> ignore) $"Reject malformed or non-object input: {malformed}"
            for trailing in ["{}"; "[]"; "true"; "42"; "\"text\""; "garbage"; "/*comment*/ {}"; "//comment\n []"] do
                Expect.throwsT<JsonReaderException> (fun () -> parse (json + " " + trailing) |> ignore) $"Reject additional content after a valid layout: {trailing}"
            Expect.throwsT<System.ArgumentNullException> (fun () -> parse null |> ignore) "Null input still rejects"
        }
    ]

    let private controls = [
        test "Explicit booleans and header positions retain their meaning" {
            Expect.equal (parse fixture) (expectedLayout mainElement) "False options and populated layout must not be replaced by defaults"
            for name, header in ["top", Header.Top; "left", Header.Left; "right", Header.Right; "bottom", Header.Bottom] do
                let actual = fixture |> change (componentPath + ".header") "show" (Some $"\"{name}\"") |> parse
                Expect.equal actual (expectedLayout { mainElement with Header = Some header }) "Known header positions must remain valid"
            let hidden = fixture |> change (componentPath + ".header") "show" (Some "false") |> parse
            Expect.equal hidden (expectedLayout { mainElement with Header = None }) "An explicit false header must remain hidden"
            for path, name, expected in [
                componentPath, "isClosable", { mainElement with Closable = true }
                componentPath + ".componentState", "keepAlive", { mainElement with KeepAlive = true }
                componentPath + ".header", "close", { mainElement with Buttons = Some Buttons.Close }
                componentPath + ".header", "popout", { mainElement with Buttons = Some Buttons.Popout }
                componentPath + ".header", "maximise", { mainElement with Buttons = Some Buttons.Maximize }
            ] do
                Expect.equal (fixture |> change path name (Some "true") |> parse) (expectedLayout expected) "True flags must remain distinct from false"
        }
        test "Numeric sizes and complete popout geometry are preserved" {
            for unit, ctor in ["%", Size.Percentage; "fr", Size.Weight] do
                for number, value in ["0", 0; "9", 9; "9.75", 9] do
                    let actual =
                        fixture
                        |> change componentPath "sizeUnit" (Some $"\"{unit}\"")
                        |> change componentPath "size" (Some number)
                        |> parse
                    Expect.equal actual (expectedLayout { mainElement with Size = ctor value }) "Numeric sizes must retain their unit and truncation behavior"
            for number, value in ["0", 0; "23", 23; "23.75", 23] do
                let actual = fixture |> change componentPath "minSize" (Some number) |> parse
                Expect.equal actual (expectedLayout { mainElement with MinSize = Some value }) "Numeric minimum sizes must remain supported"
        }
        test "Wrong primitive types retain the omission fallbacks" {
            for path, name, values in componentFields @ windowFields do
                for value in values do checkOmission path name value
        }
        test "Invalid required layout types remain rejected" {
            for path in ["root"; componentPath; "openPopouts[0].root"] do
                for value in [None; Some "null"; Some "false"; Some "42"; Some "\"unknown\""; Some "\"Component\""; Some "{}"; Some "[]"] do
                    let json = fixture |> change path "type" value
                    Expect.throws (fun () -> parse json |> ignore) $"Invalid required type at {path} must not become an empty or default layout"
        }
    ]

    [<Tests>]
    let tests =
        testList "Primitives.GoldenLayout.Json" (
            [
                test "Null component options use omission fallbacks without losing structure" {
                    for path, name, _ in componentFields do checkOmission path name "null"
                }
                test "Null popout coordinates preserve unrelated geometry and content" {
                    for path, name, _ in windowFields do checkOmission path name "null"
                }
            ] @ controls @ strings
        )
