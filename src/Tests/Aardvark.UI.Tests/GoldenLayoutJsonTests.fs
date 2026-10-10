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
        testList "GoldenLayout Json Tests" (
            [
                test "Null component options use omission fallbacks without losing structure" {
                    for path, name, _ in componentFields do checkOmission path name "null"
                }
                test "Null popout coordinates preserve unrelated geometry and content" {
                    for path, name, _ in windowFields do checkOmission path name "null"
                }
            ] @ controls
        )
