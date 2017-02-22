namespace Aardvark.Base

open System.Collections
open System.Collections.Generic

type ListOperation<'a> =
    | Set of 'a
    | Remove
    
type deltalist<'a> = Map<Index, ListOperation<'a>>

//
//[<StructuredFormatDisplay("{AsString}")>]
//type deltalist<'a>(content : Map<Index, ListOperation<'a>>) =
//    
//    static let empty = deltalist<'a>(Map.empty)
//
//    static let monoid =
//        {
//            misEmpty = fun (l : deltalist<'a>) -> l.IsEmpty
//            mempty = empty
//            mappend = fun l r -> deltalist.Combine(l,r)
//        }
//
//    member x.IsEmpty = Map.isEmpty content
//
//    static member Empty = empty
//    static member Monoid = monoid
//
//    member x.Content = content
//
//    member x.Add (index : Index, op : ListOperation<'a>) =
//        deltalist(Map.add index op content)
//
//    member x.Remove(index : Index) =
//        deltalist(Map.remove index content)
//
//    member x.Map (mapping : Index -> ListOperation<'a> -> ListOperation<'b>) =
//        deltalist(Map.map mapping content)
//        
//    member x.Collect (mapping : Index -> ListOperation<'a> -> deltalist<'b>) : deltalist<'b> =
//        let mutable res = deltalist<'b>.Empty
//        for (i,op) in Map.toSeq content do
//            let r = mapping i op
//            res <- deltalist<'b>.Combine(res, r)
//        res
//
//    member x.Filter (predicate : Index -> ListOperation<'a> -> bool) =
//        deltalist (Map.filter predicate content)
//
//    static member Combine (l : deltalist<'a>, r : deltalist<'a>) : deltalist<'a> =
//        let mutable res = l
//        for (i, op) in r do
//            res <- res.Add(i, op)
//        res
//
//    override x.ToString() =
//        content |> Map.toSeq |> Seq.map (fun (i,v) -> 
//            match v with
//                | Remove -> sprintf "Remove(%A)" i
//                | Set v -> sprintf "Set(%A, %A)" i v
//        ) |> String.concat "; " |> sprintf "deltalist [%s]"
//
//    member private x.AsString = x.ToString()
//
//    interface IEnumerable with
//        member x.GetEnumerator() = (content |> Map.toSeq).GetEnumerator() :> _
//
//    interface IEnumerable<Index * ListOperation<'a>> with
//        member x.GetEnumerator() = (content  |> Map.toSeq).GetEnumerator()

module DeltaList =
    let empty<'a> = Map.empty<Index, 'a>

    let inline add (i : Index) (v : ListOperation<'a>) (l : deltalist<'a>) = l.Add(i, v)
    let inline remove (i : Index) (l : deltalist<'a>) = l.Remove(i)
    let inline toSeq (l : deltalist<'a>) = l |> Map.toSeq
    let inline toList (l : deltalist<'a>) = l |> Map.toList
    let inline toArray (l : deltalist<'a>) = l |> Map.toArray

    let inline mapi (mapping : Index -> ListOperation<'a> -> ListOperation<'b>) (l : deltalist<'a>) = Map.map mapping l

    let combine (l : deltalist<'a>) (r : deltalist<'a>) =
        Map.union l r

    let collecti (mapping : Index -> ListOperation<'a> -> deltalist<'b>) (l : deltalist<'a>) = 
        let mutable res = Map.empty
        for (KeyValue(i,v)) in l do
            let r = mapping i v
            res <- Map.union res r

        res
        
    let inline filteri (predicate : Index -> ListOperation<'a> -> bool) (l : deltalist<'a>) = Map.filter predicate l

    type private MonoidInstance<'a>() =
        static let instance : Monoid<deltalist<'a>> =
            {
                misEmpty = Map.isEmpty
                mempty = Map.empty
                mappend = Map.union
            }
        static member Instance = instance


    let monoid<'a> = MonoidInstance<'a>.Instance