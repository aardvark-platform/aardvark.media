namespace Aardvark.UI

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open FSharp.Data.Traceable

type AttributeMap<'msg>(map : amap<string, AttributeValue<'msg>>) =
    static let empty = AttributeMap<'msg>(AMap.empty)

    static member Empty = empty

    member x.AMap = map

    member x.Content = map.Content
    member x.GetReader() = map.GetReader()

module AttributeMap =

    type private AListReader<'msg>(input : alist<string * AttributeValue<'msg>>) =
        inherit AbstractReader<HashMapDelta<string, AttributeValue<'msg>>>(HashMapDelta.empty)

        let reader = input.GetReader()

        let mutable store : HashMap<string, MapExt<Index, AttributeValue<'msg>>> = HashMap.empty

        override x.Compute(token: AdaptiveToken) =
            let old = reader.State
            let ops = reader.GetChanges(token)

            let mutable deltas = HashMap.empty

            for index, op in IndexListDelta.toSeq ops do
                match op with
                | Remove ->
                    match IndexList.tryGetV index old with
                    | ValueSome (ok, _) ->
                        store <- store |> HashMap.alterV ok (fun ovs ->
                            match ovs with
                            | ValueSome ovs ->
                                let vs = ovs |> MapExt.remove index
                                if MapExt.isEmpty vs then
                                    deltas <- HashMap.add ok Remove deltas
                                    ValueNone
                                else
                                    deltas <- HashMap.add ok (Set vs) deltas
                                    ValueSome vs
                            | ValueNone ->
                                ValueNone
                        )
                    | ValueNone ->
                        ()
                | Set(k, v) ->
                    store <- store |> HashMap.alterV k (fun ovs ->
                        let ovs =
                            match ovs with
                            | ValueNone -> MapExt.empty
                            | ValueSome ovs -> ovs

                        let vs = MapExt.add index v ovs
                        deltas <- HashMap.add k (Set vs) deltas
                        ValueSome vs
                    )

            deltas |> HashMap.chooseV (fun k vs ->
                match vs with
                | Set vs ->
                    (ValueNone, vs) ||> MapExt.fold (fun s _ v ->
                        match s with
                        | ValueNone -> ValueSome v
                        | ValueSome s -> AttributeValue.combine k s v |> ValueSome
                    ) |> ValueOption.map Set
                | Remove ->
                    ValueSome Remove
            ) |> HashMapDelta.ofHashMap


    /// the empty attributes map
    let empty<'msg> = AttributeMap<'msg>.Empty

    /// creates an attribute-map with one single entry
    let single (name : string) (value : AttributeValue<'msg>) =
        AttributeMap(AMap.ofList [name, value])

    /// creates an attribute-map using all entries from the sequence (conflicts are merged)
    let ofSeq (seq : seq<string * AttributeValue<'msg>>) =
        let mutable res = HashMap.empty
        for key, value in seq do
            res <- res |> HashMap.updateV key (fun o ->
                match o with
                | ValueSome ov -> AttributeValue.combine key ov value
                | _ -> value
            )

        AttributeMap(AMap.ofHashMap res)

    /// creates an attribute-map using all entries from the list (conflicts are merged)
    let ofList (list : list<string * AttributeValue<'msg>>) =
        ofSeq list

    /// creates an attribute-map using all entries from the array (conflicts are merged)
    let ofArray (arr : array<string * AttributeValue<'msg>>) =
        ofSeq arr

    let ofAList (list : alist<string * AttributeValue<'msg>>) =
        if list.IsConstant then
            list.Content.GetValue() |> ofSeq
        else
            AttributeMap(AMap.ofReader (fun () -> AListReader<_> list))

    let ofAMap (map : amap<string, AttributeValue<'msg>>) =
        AttributeMap(map)

    let toAMap (map : AttributeMap<'msg>) =
        map.AMap

    /// merges two attribute-maps by merging all conflicting values (preferring right)
    let union (l : AttributeMap<'msg>) (r : AttributeMap<'msg>) =
        AttributeMap(AMap.unionWith AttributeValue.combine l.AMap r.AMap)

    let rec unionMany (list : list<AttributeMap<'msg>>) =
        match list with
        | [] -> empty
        | [a] -> a
        | [a;b] -> union a b
        | a :: rest -> union a (unionMany rest)

    let ofSeqCond (seq : seq<string * aval<Option<AttributeValue<'msg>>>>) =
        let mutable groups = HashMap.empty

        for key, value in seq do
            groups <- groups |> HashMap.updateV key (fun o ->
                match o with
                | ValueSome l -> l @ [value]
                | ValueNone -> [value]
            )

        let unique =
            groups |> HashMap.map (fun key values ->
                match values with
                | [v] -> v
                | many ->
                    AVal.custom (fun token ->
                        let mutable final = None
                        for e in many do
                            match e.GetValue token with
                            | Some v ->
                                match final with
                                | None -> final <- Some v
                                | Some f -> final <- Some (AttributeValue.combine key f v)
                            | _ ->
                                ()

                        final
                    )
            )

        let map = AMap.ofHashMap unique
        map |> AMap.chooseA (fun _ v -> v) |> AttributeMap


    let ofListCond (list : list<string * aval<Option<AttributeValue<'msg>>>>) =
        ofSeqCond list

    let ofArrayCond (list : array<string * aval<Option<AttributeValue<'msg>>>>) =
        ofSeqCond list


    let map (mapping : string -> AttributeValue<'a> -> AttributeValue<'b>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.map mapping map.AMap)

    let mapAttributes (mapping : AttributeValue<'a> -> AttributeValue<'b>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.map (fun _ -> mapping) map.AMap)

    let choose (mapping : string -> AttributeValue<'a> -> Option<AttributeValue<'b>>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.choose mapping map.AMap)

    let filter (predicate : string -> AttributeValue<'a> -> bool) (map : AttributeMap<'a>) =
        AttributeMap(AMap.filter predicate map.AMap)

    /// Adds the given class to the attribute map.
    let addClass (clazz: string) (attributes: AttributeMap<'msg>) =
        union attributes <| ofList ["class", AttributeValue.String clazz]

    /// Removes the given class from the attribute map.
    let removeClass (clazz: string) (attributes: AttributeMap<'msg>) =
        attributes |> map (fun name attr ->
            match name, attr with
            | "class", AttributeValue.String str ->
                String.split " " str
                |> Array.filter (String.trim >> (<>) clazz)
                |> String.concat " "
                |> AttributeValue.String

            | _, value -> value
        )