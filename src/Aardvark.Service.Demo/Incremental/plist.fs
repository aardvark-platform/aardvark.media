namespace Aardvark.Base

open System
open System.Threading
open System.Collections
open System.Collections.Generic

[<StructuredFormatDisplay("{AsString}")>]
type plist<'a>(count : int, l : Index, h : Index, content : Map<Index, 'a>) =
    
    static let empty = plist<'a>(0, Index.zero, Index.zero, Map.empty)

    static let trace =
        {
            ops = DeltaList.monoid
            empty = empty
            apply = fun a b -> a.Apply(b)
            compute = fun l r -> plist.ComputeDeltas(l,r)
            collapse = fun _ _ -> false
        }

    static member Empty = empty
    static member Trace = trace

    member internal x.MinIndex = l
    member internal x.MaxIndex = h

    member x.Count = count

    member x.Content = content

    member x.TryGet (i : Index) =
        Map.tryFind i content

    member x.Apply(deltas : deltalist<'a>) : plist<'a> * deltalist<'a> =
        let mutable res = x
        let finalDeltas =
            deltas |> DeltaList.filteri (fun i op ->
                match op with
                    | Remove -> 
                        res <- res.Remove i
                        true
                    | Set v -> 
                        match res.TryGet i with
                            | Some o when Object.Equals(o,v) -> 
                                false
                            | _ -> 
                                res <- res.Set(i,v)
                                true
            )

        res, finalDeltas

    static member ComputeDeltas(l : plist<'a>, r : plist<'a>) : deltalist<'a> =
        match l.Count, r.Count with
            | 0, 0 -> 
                DeltaList.empty

            | 0, _ -> 
                r.Content |> Map.map (fun i v -> Set v)

            | _, 0 ->
                l.Content |> Map.map (fun i v -> Remove)

            | _, _ ->
                let mutable rem = l.Content |> Map.map (fun i v -> ListOperation.Remove)

                for i, rv in Map.toSeq r.Content do
                    match Map.tryFind i l.Content with
                        | Some lv ->
                            if Object.Equals(lv, rv) then
                                rem <- DeltaList.remove i rem
                            else
                                rem <- DeltaList.add i (Set rv) rem
                        | None ->
                            rem <- DeltaList.add i (Set rv) rem
                rem

    member x.Append(v : 'a) =
        if count = 0 then
            let t = Index.after Index.zero
            plist(1, t, t, Map.ofList [t, v])
        else
            let t = Index.after h
            plist(count + 1, l, t, Map.add t v content)
        
    member x.Prepend(v : 'a) =
        if count = 0 then
            let t = Index.after Index.zero
            plist(1, t, t, Map.ofList [t, v])
        else
            let t = Index.before l
            plist(count + 1, t, h, Map.add t v content)

    member x.Set(key : Index, value : 'a) =
        if count = 0 then
            plist(1, key, key, Map.ofList [key, value])

        elif key < l then
            plist(count + 1, key, h, Map.add key value content)

        elif key > h then
            plist(count + 1, l, key, Map.add key value content)

        elif content.ContainsKey key then
            plist(count + 1, l, h, Map.add key value content)

        else
            plist(count, l, h, Map.add key value content)

    member x.Remove(key : Index) =
        if content.ContainsKey key then
            if count = 1 then empty
            else plist(count - 1, l, h, Map.remove key content)
        else
            x

    member x.RemoveAt(i : int) =
        if i >= 0 && i < count then
            let mutable index = 0
            let newContent = 
                content |> Map.filter (fun k v -> 
                    let res = index <> i
                    index <- index + 1
                    res
                )
            plist(count - 1, l, h, newContent)
        else
            x

    member x.Map(mapping : Index -> 'a -> 'b) =
        plist(count, l, h, Map.map mapping content)

    member x.AsSeq =
        content |> Map.toSeq |> Seq.map snd

    member x.AsList =
        content |> Map.toList |> List.map snd

    member x.AsArray =
        content |> Map.toArray |> Array.map snd

    override x.ToString() =
        content |> Map.toSeq |> Seq.map (snd >> sprintf "%A") |> String.concat "; " |> sprintf "plist [%s]"

    member private x.AsString = x.ToString()


    interface IEnumerable with
        member x.GetEnumerator() = new PListEnumerator<'a>((content :> seq<_>).GetEnumerator()) :> _

    interface IEnumerable<'a> with
        member x.GetEnumerator() = new PListEnumerator<'a>((content :> seq<_>).GetEnumerator()) :> _

and private PListEnumerator<'a>(r : IEnumerator<KeyValuePair<Index, 'a>>) =
    
    member x.MoveNext() =
        r.MoveNext()

    member x.Current =
        r.Current.Value

    member x.Reset() =
        r.Reset()

    interface IEnumerator with
        member x.MoveNext() = x.MoveNext()
        member x.Current = x.Current :> obj
        member x.Reset() = x.Reset()

    interface IEnumerator<'a> with
        member x.Current = x.Current
        member x.Dispose() = r.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PList =
    let empty<'a> = plist<'a>.Empty

    let inline count (list : plist<'a>) = list.Count
    let inline append (v : 'a) (list : plist<'a>) = list.Append v
    let inline prepend (v : 'a) (list : plist<'a>) = list.Prepend v
    let inline set (id : Index) (v : 'a) (list : plist<'a>) = list.Set(id, v)
    let inline remove (id : Index) (list : plist<'a>) = list.Remove(id)
    let inline removeAt (index : int) (list : plist<'a>) = list.RemoveAt(index)

    let inline toSeq (list : plist<'a>) = list :> seq<_>
    let inline toList (list : plist<'a>) = list.AsList
    let inline toArray (list : plist<'a>) = list.AsArray

    let single (v : 'a) =
        let t = Index.after Index.zero
        plist(1, t, t, Map.ofList[t, v])

    let ofSeq (seq : seq<'a>) =
        let mutable res = empty
        for e in seq do res <- append e res
        res

    let ofList (list : list<'a>) =
        ofSeq list

    let ofArray (arr : 'a[]) =
        ofSeq arr

    let inline mapi (mapping : Index -> 'a -> 'b) (list : plist<'a>) = list.Map mapping
    let inline map (mapping : 'a -> 'b) (list : plist<'a>) = list.Map (fun _ v -> mapping v)


    let trace<'a> = plist<'a>.Trace