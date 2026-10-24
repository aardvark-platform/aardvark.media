namespace Aardvark.UI.Tests

open System
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``AttributeMap Tests`` =

    let private attribute name value : string * AttributeValue<unit> =
        name, AttributeValue.String value

    let private asString = function
        | AttributeValue.String value -> value
        | _ -> failtest "Expected a string attribute"

    let private check (attributes: AttributeMap<unit>) (reader: IHashMapReader<string, AttributeValue<unit>>) expectedDelta expectedContent =
        let delta =
            reader.GetChanges AdaptiveToken.Top
            |> HashMapDelta.toList
            |> List.map (fun (name, op) ->
                name, match op with Set value -> Set (asString value) | Remove -> Remove
            )
            |> Map.ofList

        let content =
            attributes.Content |> AVal.force |> HashMap.toList
            |> List.map (fun (name, value) -> name, asString value)
            |> Map.ofList

        Expect.equal delta (Map.ofList expectedDelta) "Unexpected attribute delta"
        Expect.equal content (Map.ofList expectedContent) "Unexpected attribute content"
        Expect.isTrue (reader.GetChanges AdaptiveToken.Top).IsEmpty "Reading again without changes must be empty"

    let private combined name values =
        if name = "class" then String.concat " " values else List.last values

    let private conditionalHelpers : (string * (seq<string * aval<AttributeValue<unit> option>> -> AttributeMap<unit>)) list =
        [ "sequence", AttributeMap.ofSeqCond
          "list", Seq.toList >> AttributeMap.ofListCond
          "array", Seq.toArray >> AttributeMap.ofArrayCond ]

    let private optional value : AttributeValue<unit> option = Some (AttributeValue.String value)

    [<Tests>]
    let tests =
        testList "AttributeMap Tests" [
            test "Renaming removes the old name" {
                let input = clist [attribute "old" "one"; attribute "keep" "stable"]
                let index = input.TryGetIndex 0 |> Option.get
                let attributes = AttributeMap.ofAList input
                let reader = attributes.GetReader()
                check attributes reader ["old", Set "one"; "keep", Set "stable"] ["old", "one"; "keep", "stable"]

                for oldName, newName, value in ["old", "middle", "two"; "middle", "new", "three"] do
                    transact (fun () -> input.[index] <- attribute newName value)
                    check attributes reader [oldName, Remove; newName, Set value] [newName, value; "keep", "stable"]
            }

            test "Deletion after renaming leaves no stale attribute" {
                let input = clist [attribute "old" "one"; attribute "keep" "stable"]
                let index = input.TryGetIndex 0 |> Option.get
                let attributes = AttributeMap.ofAList input
                let reader = attributes.GetReader()
                reader.GetChanges AdaptiveToken.Top |> ignore

                transact (fun () -> input.[index] <- attribute "new" "two")
                reader.GetChanges AdaptiveToken.Top |> ignore
                transact (fun () -> input.Remove index |> ignore)
                check attributes reader ["new", Remove] ["keep", "stable"]
            }

            test "Renaming into and out of duplicate groups preserves list order" {
                for name in ["class"; "title"] do
                    for position in 0 .. 2 do
                        let values = ["left"; "middle"; "right"]
                        let input = clist (List.map (attribute name) values)
                        let index = input.TryGetIndex position |> Option.get
                        let attributes = AttributeMap.ofAList input
                        let reader = attributes.GetReader()
                        let initial = combined name values
                        check attributes reader [name, Set initial] [name, initial]

                        let remaining = values |> List.removeAt position |> combined name
                        transact (fun () -> input.[index] <- attribute "moved" "changed")
                        let delta =
                            [
                                "moved", Set "changed"
                                if remaining <> initial then name, Set remaining
                            ]
                        check attributes reader delta [name, remaining; "moved", "changed"]

                        let restored = values |> List.updateAt position "returned" |> combined name
                        transact (fun () -> input.[index] <- attribute name "returned")
                        let delta =
                            [
                                "moved", Remove
                                if restored <> remaining then name, Set restored
                            ]
                        check attributes reader delta [name, restored]

                        transact (fun () -> input.Remove index |> ignore)
                        let delta = if remaining <> restored then [name, Set remaining] else []
                        check attributes reader delta [name, remaining]
            }

            test "Batched key swaps use the previous list state" {
                for first, second in ["class", "title"; "title", "class"; "id", "title"] do
                    let input = clist [attribute first "left"; attribute second "right"]
                    let left = input.TryGetIndex 0 |> Option.get
                    let right = input.TryGetIndex 1 |> Option.get
                    let attributes = AttributeMap.ofAList input
                    let reader = attributes.GetReader()
                    check attributes reader [first, Set "left"; second, Set "right"] [first, "left"; second, "right"]

                    for leftName, rightName in [second, first; first, second] do
                        transact (fun () ->
                            input.[left] <- attribute leftName "left"
                            input.[right] <- attribute rightName "right"
                        )
                        check attributes reader [leftName, Set "left"; rightName, Set "right"] [leftName, "left"; rightName, "right"]

                    transact (fun () -> input.Clear())
                    check attributes reader [first, Remove; second, Remove] []
            }

            test "Same-name updates keep class order and ordinary right bias" {
                for name in ["class"; "title"] do
                    for position in 0 .. 2 do
                        let values = ["left"; "middle"; "right"]
                        let input = clist (List.map (attribute name) values)
                        let index = input.TryGetIndex position |> Option.get
                        let attributes = AttributeMap.ofAList input
                        let reader = attributes.GetReader()
                        let initial = combined name values
                        check attributes reader [name, Set initial] [name, initial]

                        transact (fun () -> input.[index] <- attribute name "changed")
                        let expected = values |> List.updateAt position "changed" |> combined name
                        let delta = if expected <> initial then [name, Set expected] else []
                        check attributes reader delta [name, expected]
            }

            test "Constant lists retain duplicate merging" {
                let input = AList.ofList [attribute "class" "left"; attribute "title" "old"; attribute "class" "right"; attribute "title" "new"]
                let attributes = AttributeMap.ofAList input
                Expect.isTrue attributes.AMap.IsConstant "A constant list must remain constant"
                check attributes (attributes.GetReader()) ["class", Set "left right"; "title", Set "new"] ["class", "left right"; "title", "new"]
            }

            test "Conditional helpers enumerate once and evaluate duplicate groups in input order" {
                for helperName, create in conditionalHelpers do
                    let names = [|"class"; "title"; "style"; "class"; "title"; "style"; "class"; "title"; "style"|]
                    let labels = [|"left"; "old"; "color:red"; "middle"; "intermediate"; "display:block"; "right"; "new"; "opacity:1"|]
                    let cells = labels |> Array.map (optional >> cval)
                    let reads = ResizeArray<int>()
                    let values = cells |> Array.mapi (fun i cell -> AVal.custom (fun token -> reads.Add i; cell.GetValue token))
                    let mutable enumerations, disposals = 0, 0
                    let input = seq {
                        enumerations <- enumerations + 1
                        try
                            for i in 0 .. names.Length - 1 do yield names.[i], values.[i]
                        finally disposals <- disposals + 1
                    }
                    let attributes = create input
                    let reader = attributes.GetReader()
                    Expect.equal (enumerations, disposals) (1, 1) (helperName + ": enumerate and dispose once")
                    Expect.isEmpty reads (helperName + ": construction and reader creation must not evaluate values")
                    let expected = ["class", "left middle right"; "style", "color:red; display:block; opacity:1"; "title", "new"]
                    check attributes reader (expected |> List.map (fun (name, value) -> name, Set value)) expected
                    for name in ["class"; "title"; "style"] do
                        let expectedReads = [0..8] |> List.filter (fun i -> names.[i] = name)
                        Expect.equal (reads |> Seq.filter (fun i -> names.[i] = name) |> Seq.toList) expectedReads (helperName + ": per-name read order")
                    reads.Clear()
                    transact (fun () -> cells.[0].Value <- optional "L"; cells.[3].Value <- optional "M"; cells.[6].Value <- optional "R")
                    check attributes reader ["class", Set "L M R"] ["class", "L M R"; "style", "color:red; display:block; opacity:1"; "title", "new"]
                    Expect.equal (reads |> Seq.toList) [0; 3; 6] (helperName + ": invalidation preserves read order without evaluating unrelated groups")
                    Expect.equal (enumerations, disposals) (1, 1) (helperName + ": updates do not enumerate input again")
            }

            test "Conditional optional values activate disappear and reactivate in order" {
                for _, create in conditionalHelpers do
                    for name in ["class"; "style"; "title"] do
                        let cells = Array.init 3 (fun _ -> cval (None : AttributeValue<unit> option))
                        let attributes = create [name, cells.[0]; "keep", AVal.constant (optional "stable"); name, cells.[1]; name, cells.[2]]
                        let reader = attributes.GetReader()
                        check attributes reader ["keep", Set "stable"] ["keep", "stable"]
                        for values in [[Some "left"; None; None]; [Some "left"; None; Some "right"];
                                       [Some "left"; Some "middle"; Some "right"]; [Some "left"; Some "middle"; None];
                                       [None; Some "middle"; None]; [None; None; None]; [None; None; Some "returned"];
                                       [Some "first-again"; None; Some "returned"]] do
                            transact (fun () -> List.iteri (fun i value -> cells.[i].Value <- Option.bind optional value) values)
                            let expected =
                                match List.choose id values with
                                | [] -> None
                                | present -> Some (match name with "class" -> String.concat " " present | "style" -> String.concat "; " present | _ -> List.last present)
                            let delta = [name, match expected with Some value -> Set value | None -> Remove]
                            let content = ["keep", "stable"] @ (expected |> Option.toList |> List.map (fun value -> name, value))
                            check attributes reader delta content
            }

            test "Empty and singleton conditional groups retain deferred reads and failures" {
                for helperName, create in conditionalHelpers do
                    let empty = create Seq.empty
                    check empty (empty.GetReader()) [] []
                    let source = cval (None : AttributeValue<unit> option)
                    let mutable reads = 0
                    let value = AVal.custom (fun token -> reads <- reads + 1; source.GetValue token)
                    let singleton = create ["title", value]
                    Expect.equal reads 0 (helperName + ": singleton is deferred")
                    let reader = singleton.GetReader()
                    check singleton reader [] []
                    for current, delta, content in [optional "one", ["title", Set "one"], ["title", "one"];
                                                   None, ["title", Remove], []; optional "again", ["title", Set "again"], ["title", "again"]] do
                        transact (fun () -> source.Value <- current)
                        check singleton reader delta content
                    Expect.equal reads 4 (helperName + ": singleton reads only after invalidation")
                    let failure = InvalidOperationException "conditional value failure"
                    let order = ResizeArray<string>()
                    let observed tag result = AVal.custom (fun _ -> order.Add tag; result ())
                    let failing = create ["title", observed "left" (fun () -> optional "left");
                                          "title", observed "middle" (fun () -> raise failure);
                                          "title", observed "right" (fun () -> optional "right")]
                    Expect.isEmpty order (helperName + ": duplicate computations are deferred")
                    try
                        AVal.force failing.Content |> ignore
                        failtest "Expected deferred failure"
                    with error -> Expect.isTrue (Object.ReferenceEquals(error, failure)) "Preserve the original exception"
                    Expect.equal (order |> Seq.toList) ["left"; "middle"] "Stop at the first failed read in input order"
            }

            test "Conditional handlers preserve composition and server dispatch order" {
                for _, create in conditionalHelpers do
                    let calls = ResizeArray<string>()
                    let event tag = AttributeValue.Event (Event.ofDynamicArgs [tag] (fun args -> calls.Add tag; Expect.equal args [tag] "Original handler arguments"; Seq.singleton ()))
                    let attributes = create ["onready", AVal.constant (Some (event "first")); "title", AVal.constant (optional "interleaved");
                                             "onready", AVal.constant None; "onready", AVal.constant (Some (event "second")); "onready", AVal.constant (Some (event "third"))]
                    let handler =
                        match HashMap.find "onready" (AVal.force attributes.Content) with
                        | AttributeValue.Event value -> value
                        | _ -> failwith "Expected combined event"
                    let emitted = ResizeArray<string * string list>()
                    // Pure F# generation/dispatch protocol, not execution of browser-defined semantics.
                    handler.clientSide (fun id args -> emitted.Add (id, args); "") "element" |> ignore
                    Expect.equal (emitted |> Seq.toList) ["element", ["0"; "0"; "first"]; "element", ["0"; "1"; "second"]; "element", ["1"; "third"]] "Left-associated handler ordering"
                    for id, args in emitted do
                        Expect.equal (handler.serverSide Guid.Empty id args |> Seq.toList) [()] "Dispatch exactly one selected handler"
                    Expect.equal (calls |> Seq.toList) ["first"; "second"; "third"] "Server handler order"
                    calls.Clear()
                    let render tag = AttributeValue.RenderEvent (fun _ -> calls.Add ("call:" + tag); seq { calls.Add ("yield:" + tag); yield () })
                    let renderMap = create (["first"; "second"; "third"] |> List.map (fun tag -> "render", AVal.constant (Some (render tag))))
                    match HashMap.find "render" (AVal.force renderMap.Content) with
                    | AttributeValue.RenderEvent handler ->
                        let messages = handler Unchecked.defaultof<RenderClientInfo>
                        Expect.equal (calls |> Seq.toList) ["call:first"; "call:second"; "call:third"] "Render callback evaluation order"
                        Expect.equal (messages |> Seq.toList) [(); (); ()] "Render messages"
                        Expect.equal (calls |> Seq.toList) ["call:first"; "call:second"; "call:third"; "yield:first"; "yield:second"; "yield:third"] "Render message enumeration order"
                    | _ -> failtest "Expected combined render event"
            }
        ]
