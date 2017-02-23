namespace Aardvark.Base.Incremental

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base.Incremental
open Aardvark.Base

type IListReader<'a> = IOpReader<plist<'a>, deltalist<'a>>

type alist<'a> =
    abstract member IsConstant  : bool
    abstract member Content     : IMod<plist<'a>>
    abstract member GetReader   : unit -> IListReader<'a>

[<StructuredFormatDisplay("{AsString}")>]
type clist<'a>(initial : seq<'a>) =
    let history = History PList.trace
    do 
        let mutable last = Index.zero
        let ops = 
            initial |> Seq.map (fun v ->
                let t = Index.after last
                last <- t
                t, Set v
            )
            |> DeltaList.ofSeq

        history.Perform ops |> ignore

    member x.Count = 
        lock x (fun () -> history.State.Count)

    member x.Clear() =
        lock x (fun () ->
            if x.Count > 0 then
                let deltas = history.State.Content |> MapExt.map (fun k v -> Remove) |> DeltaList.ofMap
                history.Perform deltas |> ignore
        )

    member x.Append(v : 'a) =
        lock x (fun () ->
            let t = Index.after history.State.MaxIndex
            history.Perform (DeltaList.ofList [t, Set v]) |> ignore
            t
        )

    member x.Prepend(v : 'a) =
        lock x (fun () ->
            let t = Index.before history.State.MinIndex
            history.Perform (DeltaList.ofList [t, Set v]) |> ignore
            t
        )

    member x.Remove(i : Index) =
        lock x (fun () ->
            history.Perform (DeltaList.ofList [i, Remove])
        )

    member x.RemoveAt(i : int) =
        lock x (fun () ->
            let (id,_) = history.State.Content |> MapExt.item i
            x.Remove id |> ignore
        )

    member x.IndexOf(v : 'a) =
        lock x (fun () ->
            history.State 
                |> Seq.tryFindIndex (Unchecked.equals v) 
                |> Option.defaultValue -1
        )

    member x.Remove(v : 'a) =
        lock x (fun () ->
            match x.IndexOf v with
                | -1 -> false
                | i -> x.RemoveAt i; true
        )

    member x.Contains(v : 'a) =
        lock x (fun () -> history.State) |> Seq.exists (Unchecked.equals v)

    member x.Insert(i : int, value : 'a) =
        lock x (fun () ->
            if i < 0 || i > history.State.Content.Count then
                raise <| IndexOutOfRangeException()

            let l, s, r = MapExt.neighboursAt i history.State.Content
            let r = 
                match s with
                    | Some s -> Some s
                    | None -> r
            let index = 
                match l, r with
                    | Some (before,_), Some (after,_) -> Index.between before after
                    | None,            Some (after,_) -> Index.before after
                    | Some (before,_), None           -> Index.after before
                    | None,            None           -> Index.after Index.zero
            history.Perform (DeltaList.ofList [index, Set value]) |> ignore
            index
        )
        
    member x.CopyTo(arr : 'a[], i : int) = 
        let state = lock x (fun () -> history.State )
        state.CopyTo(arr, i)

    member x.Item
        with get (i : int) = 
            let state = lock x (fun () -> history.State)
            state.[i]

        and set (i : int) (v : 'a) =
            lock x (fun () ->
                let k = history.State.Content |> MapExt.tryItem i
                match k with
                    | Some (id,_) -> history.Perform(DeltaList.ofList [id, Set v]) |> ignore
                    | None -> ()
            )

    member x.Item
        with get (i : Index) =
            let state = lock x (fun () -> history.State)
            state.[i]

        and set (i : Index) (v : 'a) =
            lock x (fun () ->
                history.Perform(DeltaList.ofList [i, Set v]) |> ignore
            )

    override x.ToString() =
        let suffix =
            if x.Count > 5 then "; ..."
            else ""

        let content =
            history.State |> Seq.truncate 5 |> Seq.map (sprintf "%A") |> String.concat "; "

        "clist [" + content + suffix + "]"

    member private x.AsString = x.ToString()

    interface alist<'a> with
        member x.IsConstant = false
        member x.Content = history :> IMod<_>
        member x.GetReader() = history.NewReader()

    interface ICollection<'a> with 
        member x.Add(v) = x.Append v |> ignore
        member x.Clear() = x.Clear()
        member x.Remove(v) = x.Remove v
        member x.Contains(v) = x.Contains v
        member x.CopyTo(arr,i) = x.CopyTo(arr, i)
        member x.IsReadOnly = false
        member x.Count = x.Count

    interface IList<'a> with
        member x.RemoveAt(i) = x.RemoveAt i
        member x.IndexOf(item : 'a) = x.IndexOf item
        member x.Item
            with get(i : int) = x.[i]
            and set (i : int) (v : 'a) = x.[i] <- v
        member x.Insert(i,v) = x.Insert(i,v) |> ignore

    interface IEnumerable with
        member x.GetEnumerator() = (history.State :> seq<_>).GetEnumerator() :> _

    interface IEnumerable<'a> with
        member x.GetEnumerator() = (history.State :> seq<_>).GetEnumerator() :> _

module AList =
    
    [<AutoOpen>]
    module private Implementation = 
        type EmptyReader<'a> private() =
            inherit ConstantObject()

            static let instance = new EmptyReader<'a>() :> IOpReader<_,_>
            static member Instance = instance

            interface IOpReader<deltalist<'a>> with
                member x.Dispose() =
                    ()

                member x.GetOperations caller =
                    DeltaList.empty

            interface IOpReader<plist<'a>, deltalist<'a>> with
                member x.State = PList.empty

        type EmptyList<'a> private() =
            let content = Mod.constant PList.empty

            static let instance = EmptyList<'a>() :> alist<_>

            static member Instance = instance

            interface alist<'a> with
                member x.IsConstant = true
                member x.Content = content
                member x.GetReader() = EmptyReader.Instance

        type ConstantList<'a>(content : Lazy<plist<'a>>) =
            let deltas = lazy ( plist.ComputeDeltas(PList.empty, content.Value) )
            let mcontent = ConstantMod<plist<'a>>(content) :> IMod<_>

            interface alist<'a> with
                member x.IsConstant = true
                member x.GetReader() = new History.Readers.ConstantReader<_,_>(PList.trace, deltas, content) :> IListReader<_>
                member x.Content = mcontent
        
            new(content : plist<'a>) = ConstantList<'a>(Lazy.CreateFromValue content)

        type AdaptiveList<'a>(newReader : unit -> IOpReader<deltalist<'a>>) =
            let h = History.ofReader PList.trace newReader
            interface alist<'a> with
                member x.IsConstant = false
                member x.Content = h :> IMod<_>
                member x.GetReader() = h.NewReader()
                
        let inline alist (f : Ag.Scope -> #IOpReader<deltalist<'a>>) =
            let scope = Ag.getContext()
            AdaptiveList<'a>(fun () -> f scope :> IOpReader<_>) :> alist<_>
            
        let inline constant (l : Lazy<plist<'a>>) =
            ConstantList<'a>(l) :> alist<_>

    [<AutoOpen>]
    module private Readers =
        open System.Collections.Generic

        type IndexMapping() =
            static let comparer =
                { new IComparer<Index * Index * Index> with
                    member x.Compare((lo,li,_),(ro,ri,_)) =
                        let c = compare lo ro
                        if c = 0 then compare li ri
                        else c
                }

            let store = SortedSetExt<Index * Index * Index>(comparer)

            member x.Invoke(o : Index, i : Index) =
                let (l,s,r) = store.FindNeighbours((o, i, Unchecked.defaultof<_>))
                let s = if s.HasValue then Some s.Value else None

                match s with
                    | Some(_,_,i) -> 
                        i

                    | None ->
                        let l = if l.HasValue then Some l.Value else None
                        let r = if r.HasValue then Some r.Value else None

                        let result = 
                            match l, r with
                                | None, None                    -> Index.after Index.zero
                                | Some(_,_,l), None             -> Index.after l
                                | None, Some(_,_,r)             -> Index.before r
                                | Some (_,_,l), Some(_,_,r)     -> Index.between l r

                        store.Add((o, i, result)) |> ignore

                        result

            member x.Revoke(o : Index, i : Index) =
                let (l,s,r) = store.FindNeighbours((o, i, Unchecked.defaultof<_>))

                if s.HasValue then
                    store.Remove s.Value |> ignore
                    let (_,_,s) = s.Value
                    s
                else
                    failwith "[AList] removing unknown index"

            member x.Clear() =
                store.Clear()

        type IndexCache<'a, 'b>(f : Index -> 'a -> 'b, release : 'b -> unit) =
            let store = Dictionary<Index, 'a * 'b>()

            member x.Invoke(i : Index, a : 'a) =
                match store.TryGetValue(i) with
                    | (true, (oa, res)) ->
                        if Unchecked.equals oa a then
                            res
                        else
                            release res
                            let res = f i a
                            store.[i] <- (a, res)
                            res
                    | _ ->
                        let res = f i a
                        store.[i] <- (a, res)
                        res
                        
            member x.Revoke(i : Index) =
                match store.TryGetValue i with
                    | (true, (oa,ob)) -> 
                        release ob
                        ob
                    | _ -> 
                        failwithf "[AList] cannot revoke unknown index %A" i

            member x.Clear() =
                store.Values |> Seq.iter (snd >> release)
                store.Clear()

            new(f : Index -> 'a -> 'b) = IndexCache(f, ignore)

        type MapReader<'a, 'b>(scope : Ag.Scope, input : alist<'a>, mapping : Index -> 'a -> 'b) =
            inherit AbstractReader<deltalist<'b>>(scope, DeltaList.monoid)

            let r = input.GetReader()

            override x.Release() =
                r.Dispose()

            override x.Compute() =
                r.GetOperations x |> DeltaList.map (fun i op ->
                    match op with
                        | Remove -> Remove
                        | Set v -> Set (mapping i v)
                )

        type ChooseReader<'a, 'b>(scope : Ag.Scope, input : alist<'a>, mapping : Index -> 'a -> Option<'b>) =
            inherit AbstractReader<deltalist<'b>>(scope, DeltaList.monoid)

            let r = input.GetReader()
            let mapping = IndexCache mapping

            override x.Release() =
                mapping.Clear()
                r.Dispose()

            override x.Compute() =
                r.GetOperations x |> DeltaList.choose (fun i op ->
                    match op with
                        | Remove -> 
                            match mapping.Revoke(i) with
                                | Some _ -> Some Remove
                                | _ -> None
                        | Set v -> 
                            match mapping.Invoke(i, v) with
                                | Some res -> Some (Set res)
                                | _ -> None
                )

        type FilterReader<'a>(scope : Ag.Scope, input : alist<'a>, mapping : Index -> 'a -> bool) =
            inherit AbstractReader<deltalist<'a>>(scope, DeltaList.monoid)

            let r = input.GetReader()
            let mapping = IndexCache mapping

            override x.Release() =
                mapping.Clear()
                r.Dispose()

            override x.Compute() =
                r.GetOperations x |> DeltaList.choose (fun i op ->
                    match op with
                        | Remove -> 
                            match mapping.Revoke(i) with
                                | true -> Some Remove
                                | _ -> None
                        | Set v -> 
                            match mapping.Invoke(i, v) with
                                | true -> Some (Set v)
                                | _ -> None
                )


        type MultiReader<'a>(scope : Ag.Scope, mapping : IndexMapping, list : alist<'a>, release : alist<'a> -> unit) =
            inherit AbstractReader<deltalist<'a>>(scope, DeltaList.monoid)
            
            let targets = HashSet<Index>()

            let mutable reader = None

            let getReader() =
                match reader with
                    | Some r -> r
                    | None ->
                        let r = list.GetReader()
                        reader <- Some r
                        r

            member x.AddTarget(oi : Index) =
                if targets.Add oi then
                    getReader().State.Content
                        |> MapExt.mapMonotonic (fun ii v -> mapping.Invoke(oi, ii), Set v)
                        |> DeltaList.ofMap
                else
                    DeltaList.empty

            member x.RemoveTarget(dirty : HashSet<MultiReader<'a>>, oi : Index) =
                if targets.Remove oi then
                    match reader with
                        | Some r ->
                            let result = 
                                r.State.Content 
                                    |> MapExt.mapMonotonic (fun ii v -> mapping.Revoke(oi,ii), Remove)
                                    |> DeltaList.ofMap

                            if targets.Count = 0 then 
                                dirty.Remove x |> ignore
                                x.Dispose()

                            result

                        | None ->
                            failwith "[AList] invalid reader state"
                else
                    DeltaList.empty

            override x.Release() =
                match reader with
                    | Some r ->
                        release(list)
                        r.Dispose()
                        reader <- None
                    | None -> ()

            override x.Compute() =
                match reader with
                    | Some r -> 
                        let ops = r.GetOperations x

                        ops |> DeltaList.collect (fun ii op ->
                            match op with
                                | Remove -> 
                                    targets
                                        |> Seq.map (fun oi -> mapping.Revoke(oi, ii), Remove)
                                        |> DeltaList.ofSeq

                                | Set v ->
                                    targets
                                        |> Seq.map (fun oi -> mapping.Invoke(oi, ii), Set v)
                                        |> DeltaList.ofSeq

                        )

                    | None ->
                        DeltaList.empty

        type CollectReader<'a, 'b>(scope : Ag.Scope, input : alist<'a>, f : Index -> 'a -> alist<'b>) =
            inherit AbstractDirtyReader<MultiReader<'b>, deltalist<'b>>(scope, DeltaList.monoid)
            
            let mapping = IndexMapping()
            let cache = Dictionary<Index, 'a * alist<'b>>()
            let readers = Dictionary<alist<'b>, MultiReader<'b>>()
            let input = input.GetReader()

            let removeReader (l : alist<'b>) =
                readers.Remove l |> ignore

            let getReader (l : alist<'b>) =
                match readers.TryGetValue l with
                    | (true, r) -> r
                    | _ ->
                        let r = new MultiReader<'b>(scope, mapping, l, removeReader)
                        readers.Add(l, r) 
                        r

            member x.Invoke (dirty : HashSet<MultiReader<'b>>, i : Index, v : 'a) =
                match cache.TryGetValue(i) with
                    | (true, (oldValue, oldList)) ->
                        if Unchecked.equals oldValue v then
                            dirty.Add (getReader(oldList)) |> ignore
                            DeltaList.empty
                        else
                            let newList = f i v
                            cache.[i] <- (v, newList)
                            let newReader = getReader(newList)

                            let add = newReader.AddTarget i
                            let rem = getReader(oldList).RemoveTarget(dirty, i)
                            dirty.Add newReader |> ignore
                            DeltaList.combine add rem

                    | _ ->
                        let newList = f i v
                        cache.[i] <- (v, newList)
                        let newReader = getReader(newList)
                        let add = newReader.AddTarget i
                        dirty.Add newReader |> ignore
                        add

            member x.Revoke (dirty : HashSet<MultiReader<'b>>, i : Index) =
                match cache.TryGetValue i with
                    | (true, (v,l)) ->
                        let r = getReader l
                        cache.Remove i |> ignore
                        r.RemoveTarget(dirty, i)
                    | _ ->
                        failwithf "[AList] cannot remove %A from outer list" i

            override x.Release() =
                mapping.Clear()
                cache.Clear()
                readers.Values |> Seq.iter (fun r -> r.Dispose())
                readers.Clear()
                input.Dispose()

            override x.Compute(dirty) =
                let mutable result = 
                    input.GetOperations x |> DeltaList.collect (fun i op ->
                        match op with
                            | Remove -> x.Revoke(dirty,i)
                            | Set v -> x.Invoke(dirty, i, v)
                    )

                for d in dirty do
                    result <- DeltaList.combine result (d.GetOperations x)

                result

        type ConcatReader<'a>(scope : Ag.Scope, input : alist<alist<'a>>) =
            inherit AbstractDirtyReader<MultiReader<'a>, deltalist<'a>>(scope, DeltaList.monoid)
            
            let cache = Dictionary<Index, alist<'a>>()
            let mapping = IndexMapping()
            let readers = Dictionary<alist<'a>, MultiReader<'a>>()
            let input = input.GetReader()

            let removeReader (l : alist<'a>) =
                readers.Remove l |> ignore

            let getReader (l : alist<'a>) =
                match readers.TryGetValue l with
                    | (true, r) -> r
                    | _ ->
                        let r = new MultiReader<'a>(scope, mapping, l, removeReader)
                        readers.Add(l, r) 
                        r

            member x.Invoke (dirty : HashSet<MultiReader<'a>>, i : Index, newList : alist<'a>) =
                match cache.TryGetValue(i) with
                    | (true, oldList) ->
                        if oldList = newList then
                            dirty.Add (getReader(oldList)) |> ignore
                            DeltaList.empty
                        else
                            cache.[i] <- newList
                            let newReader = getReader(newList)
                            let add = newReader.AddTarget i
                            let rem = getReader(oldList).RemoveTarget(dirty, i)
                            dirty.Add newReader |> ignore
                            DeltaList.combine add rem

                    | _ ->
                        cache.[i] <- newList
                        let newReader = getReader(newList)
                        let add = newReader.AddTarget i
                        dirty.Add newReader |> ignore
                        add

            member x.Revoke (dirty : HashSet<MultiReader<'a>>, i : Index) =
                match cache.TryGetValue i with
                    | (true, l) ->
                        let r = getReader l
                        cache.Remove i |> ignore
                        r.RemoveTarget(dirty, i)
                    | _ ->
                        failwithf "[AList] cannot remove %A from outer list" i

            override x.Release() =
                mapping.Clear()
                cache.Clear()
                readers.Values |> Seq.iter (fun r -> r.Dispose())
                readers.Clear()
                input.Dispose()

            override x.Compute(dirty) =
                let mutable result = 
                    input.GetOperations x |> DeltaList.collect (fun i op ->
                        match op with
                            | Remove -> x.Revoke(dirty,i)
                            | Set v -> x.Invoke(dirty, i, v)
                    )

                for d in dirty do
                    result <- DeltaList.combine result (d.GetOperations x)

                result

        type ConcatListReader<'a>(scope : Ag.Scope, inputs : plist<alist<'a>>) =
            inherit AbstractDirtyReader<MultiReader<'a>, deltalist<'a>>(scope, DeltaList.monoid)
            
            let mapping = IndexMapping()
            let readers = Dictionary<alist<'a>, MultiReader<'a>>()

            let removeReader (l : alist<'a>) =
                readers.Remove l |> ignore

            let getReader (l : alist<'a>) =
                match readers.TryGetValue l with
                    | (true, r) -> r
                    | _ ->
                        let r = new MultiReader<'a>(scope, mapping, l, removeReader)
                        readers.Add(l, r) 
                        r            

            do inputs.Content |> MapExt.iter (fun oi l -> getReader(l).AddTarget oi |> ignore)
            let mutable initial = true



            override x.Release() =
                mapping.Clear()
                readers.Values |> Seq.iter (fun r -> r.Dispose())
                readers.Clear()
                readers.Clear()

            override x.Compute(dirty) =
                if initial then
                    initial <- false
                    readers.Values |> Seq.fold (fun d r -> DeltaList.combine d (r.GetOperations x)) DeltaList.empty

                else    
                    dirty |> Seq.fold (fun d r -> DeltaList.combine d (r.GetOperations x)) DeltaList.empty



    /// the empty alist
    let empty<'a> = EmptyList<'a>.Instance
    
    /// creates a new alist containing only the given element
    let single (v : 'a) =
        ConstantList(PList.single v) :> alist<_>
        
    /// creates a new alist using the given list
    let ofPList (l : plist<'a>) =
        ConstantList(l) :> alist<_>

    /// creates a new alist using the given sequence
    let ofSeq (s : seq<'a>) =
        s |> PList.ofSeq |> ofPList

    /// creates a new alist using the given sequence
    let ofList (l : list<'a>) =
        l |> PList.ofList |> ofPList

    /// creates a new alist using the given sequence
    let ofArray (l : 'a[]) =
        l |> PList.ofArray |> ofPList

    let concat' (lists : plist<alist<'a>>) =
        // TODO: when all constant -> result constant
        alist <| fun scope -> new ConcatListReader<'a>(scope, lists)

    let concat (lists : alist<alist<'a>>) =
        if lists.IsConstant then
            concat' (Mod.force lists.Content)
        else
            alist <| fun scope -> new ConcatReader<'a>(scope, lists)

    let mapi (mapping : Index -> 'a -> 'b) (list : alist<'a>) =
        if list.IsConstant then
            constant <| lazy (list.Content |> Mod.force |> PList.mapi mapping)
        else
            alist <| fun scope -> new MapReader<'a, 'b>(scope, list, mapping)

    let map (mapping : 'a -> 'b) (list : alist<'a>) =
        mapi (fun _ v -> mapping v) list

    let choosei (mapping : Index -> 'a -> Option<'b>) (list : alist<'a>) =
        if list.IsConstant then
            constant <| lazy (list.Content |> Mod.force |> PList.choosei mapping)
        else
            alist <| fun scope -> new ChooseReader<'a, 'b>(scope, list, mapping)

    let choose (mapping : 'a -> Option<'b>) (list : alist<'a>) =
        choosei (fun _ v -> mapping v) list

    let filteri (predicate : Index -> 'a -> bool) (list : alist<'a>) =
        if list.IsConstant then
            constant <| lazy (list.Content |> Mod.force |> PList.filteri predicate)
        else
            alist <| fun scope -> new FilterReader<'a>(scope, list, predicate)

    let filter (predicate : 'a -> bool) (list : alist<'a>) =
        filteri (fun _ v -> predicate v) list

    let collecti (mapping : Index -> 'a -> alist<'b>) (list : alist<'a>) =
        if list.IsConstant then
            concat' (list.Content |> Mod.force |> PList.mapi mapping)
        else
            alist <| fun scope -> new CollectReader<'a, 'b>(scope, list, mapping)

    let collect (mapping : 'a -> alist<'b>) (list : alist<'a>) =
        collecti (fun _ v -> mapping v) list
