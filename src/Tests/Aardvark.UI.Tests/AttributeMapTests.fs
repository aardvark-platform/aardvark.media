namespace Aardvark.UI.Tests

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
        ]
