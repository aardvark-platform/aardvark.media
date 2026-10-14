namespace Aardvark.UI.Tests

open System
open System.Text.Json
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``TextArea Tests`` =

    let private helpers =
        [
            "largeTextArea", fun changed -> Simple.largeTextArea changed (AVal.constant "initial")
            "largeTextArea'", fun changed ->
                Simple.largeTextArea' changed (AVal.constant "initial")
                    (AttributeMap.ofList [attribute "data-test" "textarea"])
        ]

    let private inputHandler (node : DomNode<string>) =
        match node with
        | :? TextNode<string> as textarea ->
            match textarea.Attributes.Content |> AVal.force |> HashMap.tryFind "oninput" with
            | Some (AttributeValue.Event handler) -> handler
            | _ -> failtest "Expected an oninput event"
        | _ -> failtest "Expected a textarea text node"

    let private quotedText =
        [
            "quoted", "\"quoted\""
            "leading quote", "\"leading"
            "trailing quote", "trailing\""
            "two quotes", "\"\""
            "multiple boundary quotes", "\"\"\"quoted\"\"\""
            "one quote", "\""
            "quoted multiline", "\"first\nsecond\""
        ]

    let private otherText =
        [
            "empty", ""
            "ordinary", "ordinary text"
            "whitespace", "  keep spaces  "
            "interior quotes", "first \"quoted\" last"
            "multiline", "first\n\"middle\"\nlast"
            "CRLF", "first\r\nsecond"
            "backslashes", @"C:\tmp\file\n\u0022"
            "Unicode", "Grüße λ 漢字 😀"
        ]

    let private checkText (name : string) (create : (string -> string) -> DomNode<string>) (value : string) =
        let calls = ResizeArray<string>()
        let handler = create (fun text -> calls.Add text; "changed: " + text) |> inputHandler
        let messages =
            handler.serverSide Guid.Empty "textarea" [JsonSerializer.Serialize value]
            |> Seq.toList
        Expect.equal (List.ofSeq calls) [value] $"{name}: callback receives exact text once"
        Expect.equal messages ["changed: " + value] $"{name}: callback result is the sole message"

    [<Tests>]
    let tests =
        testList "TextArea Tests" [
            test "Both helpers preserve boundary quotes and return one callback message" {
                for helperName, create in helpers do
                    for caseName, value in quotedText do
                        checkText $"{helperName}, {caseName}" create value
            }

            test "Both helpers preserve other decoded text and return one callback message" {
                for helperName, create in helpers do
                    for caseName, value in otherText do
                        checkText $"{helperName}, {caseName}" create value
            }

            test "Missing arguments produce no callback or message; extra arguments are ignored" {
                for helperName, create in helpers do
                    let calls = ResizeArray<string>()
                    let handler = create (fun text -> calls.Add text; text) |> inputHandler
                    let missing = handler.serverSide Guid.Empty "textarea" [] |> Seq.toList
                    Expect.isEmpty missing $"{helperName}: no message without a value"
                    Expect.isEmpty calls $"{helperName}: no callback without a value"
                    let messages =
                        handler.serverSide Guid.Empty "textarea" [JsonSerializer.Serialize "plain"; "ignored"]
                        |> Seq.toList
                    Expect.equal messages ["plain"] $"{helperName}: only the first argument is used"
                    Expect.equal (List.ofSeq calls) ["plain"] $"{helperName}: one callback with extra arguments"
            }

            test "Callback exceptions propagate unchanged" {
                for helperName, create in helpers do
                    let expected = InvalidOperationException "callback failed"
                    let mutable calls = 0
                    let handler = create (fun _ -> calls <- calls + 1; raise expected) |> inputHandler
                    let actual =
                        try
                            handler.serverSide Guid.Empty "textarea" [JsonSerializer.Serialize "plain"]
                            |> Seq.toList |> ignore
                            None
                        with error -> Some error
                    Expect.equal calls 1 $"{helperName}: throwing callback runs exactly once"
                    match actual with
                    | Some error -> Expect.isTrue (obj.ReferenceEquals(error, expected)) $"{helperName}: original exception"
                    | None -> failtest $"{helperName}: callback exception was swallowed"
            }
        ]
