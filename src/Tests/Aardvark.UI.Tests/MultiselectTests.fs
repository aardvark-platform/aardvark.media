namespace Aardvark.UI.Tests

open System
open System.Text.Encodings.Web
open System.Text.Json
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``Multiselect Tests`` =

    let private fixtures = [
        "clear selection", []
        "empty ID", [""]
        "comma", ["left,right"]
        "brackets", ["[inside]"]
        "quotes", ["\"quoted\""]
        "backslashes", [@"C:\tmp\file"]
        "whitespace", [" \t spaced \r\n "]
        "controls", ["a\000\001\b\f\n\r\tb"]
        "literal escapes", [@"\n\r\t\u0041"]
        "ordered special IDs", ["last,first"; ""; " [middle] "; "\"end\""]
        "ordinary", ["plain"]
        "ordered ordinary IDs", ["third"; "first"; "second"]
        "Unicode", ["Grüße λ 漢字 😀"]
    ]

    let private json values =
        JsonSerializer.Serialize<string list>(values, JsonSerializerOptions(Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping))

    let private handler simple mapper callback =
        let node =
            if simple then Html.multiselectListSimple ["entry"] callback
            else Html.multiselectList ["entry"] id text mapper callback
        let root = node :?> InnerNode<obj>
        let select = root.Children |> AList.force |> Seq.exactlyOne :?> InnerNode<obj>
        Expect.equal select.Tag "select" "the event belongs to the nested select"
        match select.Attributes.Content |> AVal.force |> HashMap.find "onchange" with
        | AttributeValue.Event event -> event
        | _ -> failtest "select onchange is not an event"

    let private mapped simple values =
        if simple then values else values |> List.map (fun value -> "mapped:" + value)

    let private checkValues simple name values =
        let trace = ResizeArray<string>()
        let callbacks = ResizeArray<string list>()
        let message = obj()
        let mapper value = trace.Add("map:" + value); "mapped:" + value
        let callback values = trace.Add "callback"; callbacks.Add values; message
        let event = handler simple mapper callback
        let messages = event.serverSide Guid.Empty "select" [json values]
        let expected = mapped simple values
        let expectedTrace = (if simple then [] else values |> List.map (fun value -> "map:" + value)) @ ["callback"]
        Expect.equal (List.ofSeq callbacks) [expected] $"{name}: exact values and one eager callback"
        Expect.equal (List.ofSeq trace) expectedTrace $"{name}: ordered mapper calls precede callback"
        let actual = messages |> Seq.toList
        Expect.equal actual.Length 1 $"{name}: exactly one message"
        Expect.isTrue (obj.ReferenceEquals(actual.Head, message)) $"{name}: callback result is returned"
        messages |> Seq.toList |> ignore
        Expect.equal (List.ofSeq callbacks) [expected] $"{name}: enumeration cannot repeat the callback"
        Expect.equal (List.ofSeq trace) expectedTrace $"{name}: enumeration cannot repeat mapping"

    let private checkArguments () =
        for simple in [false; true] do
            let trace = ResizeArray<string>()
            let callback values = trace.Add("callback:" + String.concat "/" values); obj()
            let mapper value = trace.Add("map:" + value); "mapped:" + value
            let event = handler simple mapper callback
            let messages = event.serverSide Guid.Empty "select" ["[\"first\",\"second\"]"; "not JSON"]
            let expected =
                if simple then ["callback:first/second"]
                else ["map:first"; "map:second"; "callback:mapped:first/mapped:second"]
            Expect.equal (List.ofSeq trace) expected "only the first argument is decoded, eagerly"
            Expect.equal (messages |> Seq.length) 1 "one message is produced"
            trace.Clear()
            Expect.throwsT<ArgumentException> (fun () -> event.serverSide Guid.Empty "select" [] |> ignore)
                "missing arguments retain Seq.head's immediate failure"
            Expect.isEmpty trace "missing arguments cannot invoke the mapper or callback"

    let private checkMapperFailure () =
        let failure = InvalidOperationException "mapper failure"
        let calls = ResizeArray<string>()
        let mutable callbackCount = 0
        let mapper value =
            calls.Add value
            if value = "bad" then raise failure
            "mapped:" + value
        let callback _ = callbackCount <- callbackCount + 1; obj()
        let event = handler false mapper callback
        let caught =
            try event.serverSide Guid.Empty "select" ["[\"first\",\"bad\",\"never\"]"] |> ignore; None
            with error -> Some error
        Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, failure)))
            "the original mapper exception propagates immediately"
        Expect.equal (List.ofSeq calls) ["first"; "bad"] "mapping stops at the failing value"
        Expect.equal callbackCount 0 "mapping must complete before onSelected runs"

    let private checkCallbackFailure () =
        for simple in [false; true] do
            let failure = InvalidOperationException "callback failure"
            let trace = ResizeArray<string>()
            let mapper value = trace.Add("map:" + value); "mapped:" + value
            let callback values =
                trace.Add("callback:" + String.concat "/" values)
                raise failure
            let event = handler simple mapper callback
            let caught =
                try event.serverSide Guid.Empty "select" ["[\"first\",\"second\"]"] |> ignore; None
                with error -> Some error
            Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, failure)))
                "the original callback exception propagates immediately"
            let expected =
                if simple then ["callback:first/second"]
                else ["map:first"; "map:second"; "callback:mapped:first/mapped:second"]
            Expect.equal (List.ofSeq trace) expected "all mapping precedes the single failing callback"

    [<Tests>]
    let tests =
        testList "Multiselect Tests" [
            test "JSON selections retain exact IDs and order through both helpers" {
                for simple in [false; true] do
                    for name, values in fixtures do
                        checkValues simple $"{name}, simple={simple}" values
            }
            testCase "first-argument handling and eager timing" checkArguments
            testCase "mapper exceptions" checkMapperFailure
            testCase "callback exceptions" checkCallbackFailure
        ]
