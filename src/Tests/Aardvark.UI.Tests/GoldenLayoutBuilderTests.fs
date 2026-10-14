namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.UI.Primitives.Golden
open Newtonsoft.Json.Linq
open Expecto

module ``GoldenLayout Builder Tests`` =

    let private main =
        element {
            id "main"
            title "Main"
            closable false
            header Header.Bottom
            buttons Buttons.None
            minSize 17
            size 37
            keepAlive false
        }

    let private secondary = element { id "secondary"; title "Secondary" }
    let private tabs = stack { main; secondary; header Header.Left; buttons Buttons.Close; size 70 }
    let private horizontal = row { main; secondary; weight 4 }
    let private vertical = column { main; secondary; size 90 }

    // Expected records do not use builders or Layout.ofRoot.
    let private expectedMain : Element =
        { Id = "main"; Title = "Main"; Closable = false; Header = Some Header.Bottom
          Buttons = Some Buttons.None; MinSize = Some 17; Size = Size.Percentage 37; KeepAlive = false }

    let private expectedSecondary : Element =
        { Id = "secondary"; Title = "Secondary"; Closable = true; Header = Some Header.Top
          Buttons = None; MinSize = None; Size = Size.Weight 1; KeepAlive = true }

    let private expectedTabs : Stack =
        { Header = Header.Left; Buttons = Some Buttons.Close; Size = Size.Percentage 70
          Content = [expectedMain; expectedSecondary] }

    let private expectedHorizontal : RowOrColumn =
        { IsRow = true; Size = Size.Weight 4; Content = [Layout.Element expectedMain; Layout.Element expectedSecondary] }

    let private expectedVertical : RowOrColumn =
        { IsRow = false; Size = Size.Percentage 90; Content = [Layout.Element expectedMain; Layout.Element expectedSecondary] }

    let private first =
        popout {
            element { id "first"; title "First"; header None; buttons Buttons.None; minSize 22; size 65; keepAlive false }
            position (V2i(-120, 30))
            size (V2i(800, 600))
        }

    let private second =
        popout {
            element { id "second"; title "Second"; weight 2 }
            position (V2i(100, 200))
            size (V2i(320, 240))
        }

    let private expectedFirst : PopoutWindow =
        { Root = Layout.Element {
            Id = "first"; Title = "First"; Closable = true; Header = None; Buttons = Some Buttons.None
            MinSize = Some 22; Size = Size.Percentage 65; KeepAlive = false }
          Position = Some (V2i(-120, 30)); Size = Some (V2i(800, 600)) }

    let private expectedSecond : PopoutWindow =
        { Root = Layout.Element {
            Id = "second"; Title = "Second"; Closable = true; Header = Some Header.Top; Buttons = None
            MinSize = None; Size = Size.Weight 2; KeepAlive = true }
          Position = Some (V2i(100, 200)); Size = Some (V2i(320, 240)) }

    let private expectedPopouts = [expectedFirst; expectedSecond]

    let inline private compositionCases name (rootInput : ^Root) (expectedRoot : Layout) =
        let expected = { Root = Some expectedRoot; PopoutWindows = expectedPopouts }
        [
            name + ", before popouts", layout { yield rootInput; yield first; yield second }, expected
            name + ", after popouts", layout { yield first; yield second; yield rootInput }, expected
            name + ", between popouts", layout { yield first; yield rootInput; yield second }, expected
            name + ", after empty fragment", layout { yield (); yield rootInput; yield first; yield second }, expected
        ]

    let private compositions =
        compositionCases "Element" main (Layout.Element expectedMain) @
        compositionCases "Stack" tabs (Layout.Stack expectedTabs) @
        compositionCases "row" horizontal (Layout.RowOrColumn expectedHorizontal) @
        compositionCases "column" vertical (Layout.RowOrColumn expectedVertical) @
        compositionCases "Layout" (Layout.Stack tabs) (Layout.Stack expectedTabs)

    let inline private operationCases name (rootInput : ^Root) (expectedRoot : Layout) =
        let expected = { Root = Some expectedRoot; PopoutWindows = expectedPopouts }
        [
            name + ", root then popouts", layout { root rootInput; popouts [first; second] }, expected
            name + ", popouts then root", layout { popouts [first; second]; root rootInput }, expected
        ]

    let private operations =
        operationCases "Element" main (Layout.Element expectedMain) @
        operationCases "Stack" tabs (Layout.Stack expectedTabs) @
        operationCases "row" horizontal (Layout.RowOrColumn expectedHorizontal) @
        operationCases "column" vertical (Layout.RowOrColumn expectedVertical) @
        operationCases "Layout" (Layout.Stack tabs) (Layout.Stack expectedTabs) @
        [
            "root replaces a yielded root",
                layout { secondary; root main; popouts [first; second] },
                { Root = Some (Layout.Element expectedMain); PopoutWindows = expectedPopouts }
            "popouts replaces a yielded popout",
                layout { second; root main; popouts [first; second] },
                { Root = Some (Layout.Element expectedMain); PopoutWindows = expectedPopouts }
            "popouts clears previously yielded popouts",
                layout { first; root main; popouts [] },
                { Root = Some (Layout.Element expectedMain); PopoutWindows = [] }
        ]

    let private emptyLayouts =
        [
            "empty", layout { yield () }, { Root = None; PopoutWindows = [] }
            "popout-only", layout { yield first; yield second }, { Root = None; PopoutWindows = expectedPopouts }
            "empty fragments around popouts", layout { yield (); yield first; yield (); yield second; yield () },
                { Root = None; PopoutWindows = expectedPopouts }
        ]

    let private multipleRoots =
        [
            "Element before Stack", layout { yield main; yield tabs; yield first; yield second },
                { Root = Some (Layout.Element expectedMain); PopoutWindows = expectedPopouts }
            "Stack before Element", layout { yield tabs; yield main; yield first; yield second },
                { Root = Some (Layout.Stack expectedTabs); PopoutWindows = expectedPopouts }
        ]

    let private popoutJson = """
        [
          {
            "root": {
              "type": "component", "componentType": "first", "title": "First", "isClosable": true,
              "header": {"show": false, "close": false, "popout": false, "maximise": false},
              "size": "65%", "minSize": "22px", "componentState": {"keepAlive": false}
            },
            "window": {"left": -120, "top": 30, "width": 800, "height": 600}
          },
          {
            "root": {
              "type": "component", "componentType": "second", "title": "Second", "isClosable": true,
              "header": {"show": "top"}, "size": "2fr", "componentState": {"keepAlive": true}
            },
            "window": {"left": 100, "top": 200, "width": 320, "height": 240}
          }
        ]
        """

    let rec private componentIds = function
        | Layout.Element e -> [e.Id]
        | Layout.Stack s -> s.Content |> List.map (fun e -> e.Id)
        | Layout.RowOrColumn rc -> rc.Content |> List.collect componentIds

    let private checkJson context (actual : WindowLayout) (expected : WindowLayout) =
        let json = GoldenLayout.Json.serialize LayoutConfig.Default actual |> JObject.Parse
        match expected.Root with
        | Some root ->
            Expect.isNotNull json.["root"] $"{context}: serialized main root"
            let ids = json.["root"].SelectTokens("$..componentType") |> Seq.map (fun token -> token.Value<string>()) |> Seq.toList
            Expect.equal ids (componentIds root) $"{context}: serialized main components"
        | None -> Expect.isNull json.["root"] $"{context}: no invented main root"
        // Populated fixtures always contain the independently specified ordered pair.
        let expectedPopouts = if List.isEmpty expected.PopoutWindows then JArray() else JArray.Parse popoutJson
        Expect.isTrue (JToken.DeepEquals(json.["openPopouts"], expectedPopouts)) $"{context}: complete serialized popout data and order"

    [<Tests>]
    let tests =
        testList "GoldenLayout Builder Tests" [
            test "Main roots and ordered popouts survive every fragment order" {
                for context, actual, expected in compositions do
                    Expect.equal actual expected $"{context}: retain main root and ordered popouts"
            }

            test "Serialization retains main components and complete popout data" {
                for context, actual, expected in compositions do
                    checkJson context actual expected
            }

            test "Empty and popout-only layouts do not acquire a main root" {
                for context, actual, expected in emptyLayouts do
                    Expect.equal actual expected $"{context}: preserve empty root and ordered popouts"
                    checkJson context actual expected
            }

            test "Multiple yielded roots keep the first root" {
                for context, actual, expected in multipleRoots do
                    Expect.equal actual expected $"{context}: first-root precedence"
                    checkJson context actual expected
            }

            test "Root and popouts custom operations retain replacement semantics" {
                for context, actual, expected in operations do
                    Expect.equal actual expected $"{context}: preserve custom operations"
                    checkJson context actual expected
            }
        ]
