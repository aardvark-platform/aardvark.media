namespace Aardvark.UI.Tests

open System
open System.Text.Json
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Expecto

module ``Dropdown Tests`` =

    [<StructuralEquality; NoComparison>]
    type private Key = { Id : int }

    [<CustomEquality; CustomComparison>]
    type private RankedKey =
        { Id : int; Rank : int }
        override x.Equals other =
            match other with
            | :? RankedKey as y -> x.Id = y.Id && x.Rank = y.Rank
            | _ -> false
        override x.GetHashCode() = HashCode.Combine(x.Id, x.Rank)
        interface IComparable with
            member x.CompareTo other = compare x.Rank (other :?> RankedKey).Rank

    type private Mode = Single | Optional | Multiple
    let private modes = [Single; Optional; Multiple]
    let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()
    let private hash key = pickler.ComputeHash(key).Hash |> Convert.ToBase64String

    let private create mode (chosen : aval<'T list>) (calls : ResizeArray<'T list>) (values : Dropdown.DropdownValues<'T, 'T list>) =
        let report keys = calls.Add keys; keys
        let attributes : Attribute<'T list> list = []
        match mode with
        | Single -> Dropdown.dropdown (fun key -> report [key]) false None (chosen |> AVal.map List.head) attributes values
        | Optional -> Dropdown.dropdownOption (Option.toList >> report) false None "Choose" (chosen |> AVal.map List.tryHead) attributes values
        | Multiple -> Dropdown.dropdownMultiSelect report false "Choose" (chosen |> AList.ofAVal) attributes values

    let private inner (node : DomNode<'T>) = node :?> InnerNode<'T>
    let private children node = (inner node).Children |> AList.force |> IndexList.toList
    let private attribute name node = HashMap.find name (AVal.force (inner node).Attributes.Content)
    let private stringAttribute name node =
        match attribute name node with
        | AttributeValue.String value -> value
        | _ -> failwith $"Expected string attribute {name}"

    let private withDropdown mode values (chosen : cval<'T list>) action =
        let calls = ResizeArray<'T list>()
        let root = create mode chosen calls values
        let menu = children root |> List.find (fun child ->
            match child with
            | :? InnerNode<'T list> -> stringAttribute "class" child = "menu"
            | _ -> false)
        let reader = (inner menu).Children.GetReader()
        use channel = root.Channels.["channelSelection"].GetReader()
        let handler =
            match attribute "data-event" root with
            | AttributeValue.Event handler -> handler
            | _ -> failwith "Expected public dropdown server handler"
        let mutable lastSelection = None
        let snapshot () =
            reader.GetChanges AdaptiveToken.Top |> ignore
            reader.State |> IndexList.toList |> List.map (fun item ->
                stringAttribute "data-value" item, children item |> List.exactlyOne)
        let check context ordered expected =
            let actual = snapshot ()
            Expect.equal actual.Length (List.length expected) (context + ": option count")
            for key, expectedNode in expected do
                let matches = actual |> List.filter (fun (value, _) -> value = hash key)
                Expect.equal matches.Length 1 (context + ": exactly one option per key")
                let _, actualNode = matches.Head
                Expect.isTrue (Object.ReferenceEquals(actualNode, expectedNode)) (context + ": current node, not retired content")
            if ordered then
                Expect.equal (actual |> List.map fst) (expected |> List.map (fst >> hash)) (context + ": option order")
            let selected =
                match mode with
                | Single | Optional -> chosen.Value |> List.truncate 1
                | Multiple -> chosen.Value
            let selected = selected |> List.filter (fun key -> expected |> List.exists (fun (k, _) -> k = key)) |> List.map hash
            let expectedMessages = if lastSelection = Some selected then [] else [Pickler.jsonToString selected]
            let messages = channel.GetMessages AdaptiveToken.Top
            Expect.equal messages expectedMessages (context + ": exact selection serialization and duplicate-message suppression")
            for message in messages do
                Expect.equal (JsonSerializer.Deserialize<string[]>(message) |> Array.toList) selected (context + ": decoded selection")
            lastSelection <- Some selected
            actual
        let receive context sent expected =
            calls.Clear()
            let args = [JsonSerializer.Serialize(String.concat "," sent)]
            let messages = handler.serverSide Guid.Empty "dropdown" args |> Seq.toList
            let result =
                match mode, expected with
                | Single, [] -> [] // Existing single-value policy drops the callback when nothing resolves.
                | (Single | Optional), first :: _ -> [[first]]
                | _ -> [expected]
            Expect.equal messages result (context + ": server messages")
            Expect.equal (calls |> Seq.toList) result (context + ": callback arguments and count")
        action root check receive

    let private mapChanges keys rank =
        for mode in modes do
            let nodes = keys |> List.mapi (fun i key -> key, (text $"initial-{i}" : DomNode<_>))
            let source = cmap nodes
            let chosen = cval (List.rev keys)
            let ordered pairs = match rank with Some projection -> pairs |> List.sortBy (fst >> projection) | None -> pairs
            let orderedCheck = rank |> Option.isSome
            withDropdown mode (Dropdown.DropdownValues.Map source) chosen (fun _ check receive ->
                let context phase = $"{mode}, {phase}"
                let mutable expected = ordered nodes
                let checkNow phase = check (context phase) orderedCheck expected
                checkNow "initial" |> ignore
                let removed = keys.[0]
                transact (fun () -> source.Remove removed |> ignore)
                expected <- expected |> List.filter (fun (key, _) -> key <> removed)
                receive (context "removed hash filtered before menu read") [hash removed; hash keys.[1]] [keys.[1]]
                checkNow "remove" |> ignore
                let replacement = text "replacement" : DomNode<_>
                transact (fun () -> source.[keys.[1]] <- replacement)
                expected <- expected |> List.map (fun (key, node) -> key, if key = keys.[1] then replacement else node)
                checkNow "replace same key" |> ignore
                receive (context "order and duplicate keys") [hash keys.[2]; hash removed; hash keys.[2]; hash keys.[1]] [keys.[2]; keys.[2]; keys.[1]]
                for key in List.tail keys do
                    transact (fun () -> source.Remove key |> ignore)
                    expected <- expected |> List.filter (fun (k, _) -> k <> key)
                    checkNow "remove remaining key" |> ignore
                receive (context "all retired hashes") (keys |> List.map hash) []
                let fresh = text "re-added" : DomNode<_>
                transact (fun () -> source.[removed] <- fresh; chosen.Value <- [removed])
                expected <- [removed, fresh]
                checkNow "re-add" |> ignore
                receive (context "re-added hash") [hash removed] [removed]
                transact (fun () -> source.Clear())
                expected <- []
                checkNow "clear" |> ignore
                receive (context "cleared hash") [hash removed] []
                transact (fun () -> source.[removed] <- fresh)
                expected <- [removed, fresh]
                checkNow "re-add after clear" |> ignore)

    [<Tests>]
    let tests =
        testList "Dropdown" [
            test "Non-comparable map keys stay synchronized through removal and replacement" {
                Expect.isFalse (typeof<IComparable>.IsAssignableFrom typeof<Key>) "Fixture must not be comparable"
                mapChanges ([0..5] |> List.map (fun i -> { Key.Id = i })) None
            }

            test "Comparable maps retain ordering and same-key replacement" {
                mapChanges [30; 10; 40; 20; 5; 35] (Some id)
            }

            test "Distinct map keys with equal comparison ranks remain distinct" {
                let keys = [2; 1; 1; 0; 1; 2] |> List.mapi (fun i rank -> { RankedKey.Id = i; Rank = rank })
                // Ordering within a tied rank is deliberately unspecified.
                for mode in modes do
                    let source = cmap (keys |> List.map (fun key -> key, (text (string key.Id) : DomNode<_>)))
                    let chosen = cval keys
                    withDropdown mode (Dropdown.DropdownValues.Map source) chosen (fun _ check receive ->
                        let mutable expected = keys |> List.map (fun key -> key, source.[key])
                        let verify phase =
                            let actual = check phase false expected
                            let rankByHash = keys |> List.map (fun key -> hash key, key.Rank) |> Map.ofList
                            let ranks = actual |> List.map (fun (value, _) -> rankByHash.[value])
                            Expect.equal ranks (List.sort ranks) (phase + ": nondecreasing rank")
                        verify "initial equal ranks"
                        let replacement = text "new equal-rank content" : DomNode<_>
                        transact (fun () -> source.[keys.[1]] <- replacement)
                        expected <- expected |> List.map (fun (key, node) -> key, if key = keys.[1] then replacement else node)
                        verify "equal-rank replacement"
                        transact (fun () -> source.Remove keys.[1] |> ignore)
                        expected <- expected |> List.filter (fun (key, _) -> key <> keys.[1])
                        verify "equal-rank removal"
                        receive "equal-rank lookup" [hash keys.[1]; hash keys.[2]] [keys.[2]])
            }

            test "List-backed options preserve supplied order and updates" {
                for mode in modes do
                    let initial = [3; 1; 2] |> List.map (fun key -> key, (text (string key) : DomNode<_>))
                    let source = clist initial
                    let chosen = cval [2; 3; 1]
                    withDropdown mode (Dropdown.DropdownValues.List source) chosen (fun _ check receive ->
                        check "initial list" true initial |> ignore
                        let index = source.TryGetIndex 1 |> Option.get
                        let replacement = text "list replacement" : DomNode<_>
                        transact (fun () -> source.[index] <- 1, replacement)
                        check "list replacement" true [initial.[0]; 1, replacement; initial.[2]] |> ignore
                        transact (fun () -> source.Remove index |> ignore)
                        check "list removal" true [initial.[0]; initial.[2]] |> ignore
                        receive "list stale hash" [hash 1; hash 3] [3]
                        transact (fun () -> source.Clear())
                        check "empty list" true [] |> ignore
                        receive "empty list lookup" [hash 2] []
                        transact (fun () -> source.Add (1, replacement) |> ignore; chosen.Value <- [1])
                        check "re-added list" true [1, replacement] |> ignore)
            }

            test "Immutable maps and optional empty selection retain their contracts" {
                for mode in modes do
                    let keys = [0..3] |> List.map (fun i -> { Key.Id = i })
                    let pairs = keys |> List.map (fun key -> key, (text (string key.Id) : DomNode<_>))
                    let chosen = cval (List.rev keys)
                    withDropdown mode (Dropdown.DropdownValues.Map (AMap.ofList pairs)) chosen (fun root check receive ->
                        check "immutable non-comparable map" false pairs |> ignore
                        check "unchanged evaluation" false pairs |> ignore
                        receive "immutable callback order" [hash keys.[1]; hash keys.[0]] [keys.[1]; keys.[0]]
                        if mode <> Single then
                            transact (fun () -> chosen.Value <- [])
                            check "empty external selection" false pairs |> ignore
                        match root.Boot with
                        | ValueSome boot -> Expect.equal (boot "widget") "const $self = $('#widget');aardvark.dropdown($self, 'click', channelSelection);" "Client generation provenance only"
                        | _ -> failtest "Missing dropdown boot")
                    let ordered = [1; 2; 3] |> List.map (fun key -> key, (text (string key) : DomNode<_>))
                    let selected = cval [3; 1]
                    withDropdown mode (Dropdown.DropdownValues.Map (AMap.ofList (List.rev ordered))) selected (fun _ check receive ->
                        check "immutable comparable map" true ordered |> ignore
                        receive "immutable comparable callback" [hash 2; hash 1] [2; 1])
            }
        ]
