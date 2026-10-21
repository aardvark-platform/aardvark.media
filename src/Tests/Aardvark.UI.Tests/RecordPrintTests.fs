namespace Aardvark.UI.Tests

open System
open System.Threading
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``Record Print Tests`` =

    let mutable private getterCalls = 0
    let private throwingGetter () : string =
        Interlocked.Increment(&getterCalls) |> ignore
        raise (InvalidOperationException "Custom getters must not be read")

    type ReferenceWithMembers =
        { Zulu : string; Amount : float; Alpha : int }
        member this.Computed = this.Alpha * 2
        static member Description = "Not a record field"
        member _.Throwing = throwingGetter()

    [<Struct>]
    type StructWithMembers =
        { Zebra : float; Alpha : string; Count : int; Enabled : bool }
        member this.Computed = this.Count + 1
        static member Description = "Not a record field"
        member _.Throwing = throwingGetter()

    type PlainReference =
        { Zulu : string; Amount : float; Alpha : int }

    [<Struct>]
    type PlainStruct =
        { Zebra : float; Alpha : string; Count : int }

    type NotARecord() =
        member _.LooksLikeAField = throwingGetter()

    let private printedText (value: obj) =
        let node : DomNode<unit> = Html.SemUi.recordPrint value
        match node with
        | :? InnerNode<unit> as label ->
            Expect.equal label.Tag "div" "Keep the label container"
            label.Children |> AList.force |> IndexList.toList
            |> List.map (fun child ->
                match child with
                | :? TextNode<unit> as text ->
                    Expect.equal text.Tag "span" "Keep each field as a text child"
                    text.Text |> AVal.force
                | _ -> failtest "Expected a text child for each record field")
        | _ -> failtest "Expected a label with children"

    let private check value expected =
        Expect.equal (printedText value) expected "Print only record fields, in declaration order, with their corresponding values"
        Expect.equal (Volatile.Read(&getterCalls)) 0 "Never invoke a custom getter"

    [<Tests>]
    let tests =
        testList "Record Print Tests" [
            test "Reference records ignore computed and static properties" {
                let record : ReferenceWithMembers = { Zulu = "reference"; Amount = 12.3456; Alpha = 7 }
                check (box record) ["Zulu: reference"; "Amount: 12.35"; "Alpha: 7"]
            }
            test "Struct records ignore computed and static properties" {
                let record : StructWithMembers = { Zebra = -2.5; Alpha = "struct"; Count = 9; Enabled = true }
                check (box record) ["Zebra: -2.50"; "Alpha: struct"; "Count: 9"; "Enabled: True"]
            }
            test "Ordinary records retain field order and formatting" {
                let references : (PlainReference * string list) list = [
                    { Zulu = "plain"; Amount = 12.3456; Alpha = 7 }, ["Zulu: plain"; "Amount: 12.35"; "Alpha: 7"]
                    { Zulu = ""; Amount = 1.0; Alpha = 0 }, ["Zulu: "; "Amount: 1.00"; "Alpha: 0"]
                    { Zulu = null; Amount = -2.5; Alpha = -7 }, ["Zulu: "; "Amount: -2.50"; "Alpha: -7"]
                ]
                for record, expected in references do check (box record) expected
                let record : PlainStruct = { Zebra = 0.0; Alpha = "ordinary struct"; Count = 42 }
                check (box record) ["Zebra: 0.00"; "Alpha: ordinary struct"; "Count: 42"]
            }
            test "Null and non-record inputs retain empty output" {
                for value in [null; box 42; box "not a record"; box (1, 2); box [|1; 2|]; box (NotARecord())] do
                    check value []
            }
        ]
