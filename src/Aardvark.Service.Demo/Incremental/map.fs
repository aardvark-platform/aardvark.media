// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Diagnostics
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators

module MapExtImplementation = 
    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    [<NoEquality; NoComparison>]
    type MapTree<'Key,'Value> = 
        | MapEmpty 
        | MapOne of 'Key * 'Value
        | MapNode of 'Key * 'Value * MapTree<'Key,'Value> *  MapTree<'Key,'Value> * int * int
            // REVIEW: performance rumour has it that the data held in MapNode and MapOne should be
            // exactly one cache line. It is currently ~7 and 4 words respectively. 

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MapTree = 

        let empty = MapEmpty 

        let height = function
            | MapEmpty -> 0
            | MapOne _ -> 1
            | MapNode(_,_,_,_,h,_) -> h

        let size = function
            | MapEmpty -> 0
            | MapOne _ -> 1
            | MapNode(_,_,_,_,_,s) -> s

        let isEmpty m = 
            match m with 
            | MapEmpty -> true
            | _ -> false

        let mk l k v r = 
            match l,r with 
            | MapEmpty,MapEmpty -> MapOne(k,v)
            | _ -> 
                let hl = height l 
                let hr = height r 
                let m = if hl < hr then hr else hl 
                MapNode(k,v,l,r,m+1, 1 + size l + size r)

        let rebalance t1 k v t2 =
            let t1h = height t1 
            let t2h = height t2 
            if  t2h > t1h + 2 then (* right is heavier than left *)
                match t2 with 
                | MapNode(t2k,t2v,t2l,t2r,_,_) -> 
                    (* one of the nodes must have height > height t1 + 1 *)
                    if height t2l > t1h + 1 then  (* balance left: combination *)
                        match t2l with 
                        | MapNode(t2lk,t2lv,t2ll,t2lr,_,_) ->
                        mk (mk t1 k v t2ll) t2lk t2lv (mk t2lr t2k t2v t2r) 
                        | _ -> failwith "rebalance"
                    else (* rotate left *)
                        mk (mk t1 k v t2l) t2k t2v t2r
                | _ -> failwith "rebalance"
            else
                if  t1h > t2h + 2 then (* left is heavier than right *)
                    match t1 with 
                    | MapNode(t1k,t1v,t1l,t1r,_,_) -> 
                        (* one of the nodes must have height > height t2 + 1 *)
                        if height t1r > t2h + 1 then 
                            (* balance right: combination *)
                            match t1r with 
                            | MapNode(t1rk,t1rv,t1rl,t1rr,_,_) ->
                                mk (mk t1l t1k t1v t1rl) t1rk t1rv (mk t1rr k v t2)
                            | _ -> failwith "rebalance"
                        else
                            mk t1l t1k t1v (mk t1r k v t2)
                    | _ -> failwith "rebalance"
                else mk t1 k v t2

        let rec add (comparer: IComparer<'Value>) k v m = 
            match m with 
            | MapEmpty -> MapOne(k,v)
            | MapOne(k2,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0   then MapNode (k,v,MapEmpty,m,2, 2)
                elif c = 0 then MapOne(k,v)
                else            MapNode (k,v,m,MapEmpty,2, 2)
            | MapNode(k2,v2,l,r,h,s) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then rebalance (add comparer k v l) k2 v2 r
                elif c = 0 then MapNode(k,v,l,r,h,s)
                else rebalance l k2 v2 (add comparer k v r) 

        let rec find (comparer: IComparer<'Value>) k m = 
            match m with 
            | MapEmpty -> raise (KeyNotFoundException())
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then v2
                else raise (KeyNotFoundException())
            | MapNode(k2,v2,l,r,_,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then find comparer k l
                elif c = 0 then v2
                else find comparer k r

        let rec tryFind (comparer: IComparer<'Value>) k m = 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then Some v2
                else None
            | MapNode(k2,v2,l,r,_,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then tryFind comparer k l
                elif c = 0 then Some v2
                else tryFind comparer k r

        let partition1 (comparer: IComparer<'Value>) (f:OptimizedClosures.FSharpFunc<_,_,_>) k v (acc1,acc2) = 
            if f.Invoke(k, v) then (add comparer k v acc1,acc2) else (acc1,add comparer k v acc2) 
        
        let rec partitionAux (comparer: IComparer<'Value>) (f:OptimizedClosures.FSharpFunc<_,_,_>) s acc = 
            match s with 
            | MapEmpty -> acc
            | MapOne(k,v) -> partition1 comparer f k v acc
            | MapNode(k,v,l,r,_,_) -> 
                let acc = partitionAux comparer f r acc 
                let acc = partition1 comparer f k v acc
                partitionAux comparer f l acc

        let partition (comparer: IComparer<'Value>) f s = partitionAux comparer (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) s (empty,empty)

        let filter1 (comparer: IComparer<'Value>) (f:OptimizedClosures.FSharpFunc<_,_,_>) k v acc = if f.Invoke(k, v) then add comparer k v acc else acc 

        let rec filterAux (comparer: IComparer<'Value>) (f:OptimizedClosures.FSharpFunc<_,_,_>) s acc = 
            match s with 
            | MapEmpty -> acc
            | MapOne(k,v) -> filter1 comparer f k v acc
            | MapNode(k,v,l,r,_,_) ->
                let acc = filterAux comparer f l acc
                let acc = filter1 comparer f k v acc
                filterAux comparer f r acc

        let filter (comparer: IComparer<'Value>) f s = filterAux comparer (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) s empty

        let rec spliceOutSuccessor m = 
            match m with 
            | MapEmpty -> failwith "internal error: MapExt.spliceOutSuccessor"
            | MapOne(k2,v2) -> k2,v2,MapEmpty
            | MapNode(k2,v2,l,r,_,_) ->
                match l with 
                | MapEmpty -> k2,v2,r
                | _ -> let k3,v3,l' = spliceOutSuccessor l in k3,v3,mk l' k2 v2 r

        let rec remove (comparer: IComparer<'Value>) k m = 
            match m with 
            | MapEmpty -> empty
            | MapOne(k2,_) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then MapEmpty else m
            | MapNode(k2,v2,l,r,_,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then rebalance (remove comparer k l) k2 v2 r
                elif c = 0 then 
                    match l,r with 
                    | MapEmpty,_ -> r
                    | _,MapEmpty -> l
                    | _ -> 
                        let sk,sv,r' = spliceOutSuccessor r 
                        mk l sk sv r'
                else rebalance l k2 v2 (remove comparer k r) 


        let rec alterAux (comparer : IComparer<'Value>) k (f:OptimizedClosures.FSharpFunc<_,_,_>) m =
            match m with   
            | MapEmpty ->
                match f.Invoke(k, None) with
                    | Some v -> MapOne(k,v)
                    | None -> MapEmpty

            | MapOne(k2, v2) ->
                let c = comparer.Compare(k,k2) 
                if c = 0 then
                    match f.Invoke(k2, Some v2) with
                        | Some v3 -> MapOne(k2, v3)
                        | None -> MapEmpty
                else
                    match f.Invoke(k, None) with
                        | None -> 
                            MapOne(k2, v2)

                        | Some v3 -> 
                            if c > 0 then MapNode (k2,v2,MapEmpty,MapOne(k, v3),2, 2)
                            else MapNode(k2, v2, MapOne(k, v3), MapEmpty, 2, 2)
            | MapNode(k2, v2, l, r, h, cnt) ->
            
                let c = comparer.Compare(k, k2)

                if c = 0 then
                    match f.Invoke(k, Some v2) with
                        | Some v3 -> 
                            MapNode(k2, v3, l, r, h, cnt)

                        | None ->
                            match l,r with 
                            | MapEmpty,_ -> r
                            | _,MapEmpty -> l
                            | _ -> 
                                let sk,sv,r' = spliceOutSuccessor r 
                                mk l sk sv r'
                elif c > 0 then
                    rebalance l k2 v2 (alterAux comparer k f r) 
                else
                    rebalance (alterAux comparer k f l)  k2 v2 r
       
        let alter (comparer: IComparer<'Value>) k f m = alterAux comparer k (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m
      

        let rec mem (comparer: IComparer<'Value>) k m = 
            match m with 
            | MapEmpty -> false
            | MapOne(k2,_) -> (comparer.Compare(k,k2) = 0)
            | MapNode(k2,_,l,r,_,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then mem comparer k l
                else (c = 0 || mem comparer k r)

        let rec iterOpt (f:OptimizedClosures.FSharpFunc<_,_,_>) m =
            match m with 
            | MapEmpty -> ()
            | MapOne(k2,v2) -> f.Invoke(k2, v2)
            | MapNode(k2,v2,l,r,_,_) -> iterOpt f l; f.Invoke(k2, v2); iterOpt f r

        let iter f m = iterOpt (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m

        let rec tryPickOpt (f:OptimizedClosures.FSharpFunc<_,_,_>) m =
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> f.Invoke(k2, v2) 
            | MapNode(k2,v2,l,r,_,_) -> 
                match tryPickOpt f l with 
                | Some _ as res -> res 
                | None -> 
                match f.Invoke(k2, v2) with 
                | Some _ as res -> res 
                | None -> 
                tryPickOpt f r

        let tryPick f m = tryPickOpt (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m

        let rec existsOpt (f:OptimizedClosures.FSharpFunc<_,_,_>) m = 
            match m with 
            | MapEmpty -> false
            | MapOne(k2,v2) -> f.Invoke(k2, v2)
            | MapNode(k2,v2,l,r,_,_) -> existsOpt f l || f.Invoke(k2, v2) || existsOpt f r

        let exists f m = existsOpt (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m

        let rec forallOpt (f:OptimizedClosures.FSharpFunc<_,_,_>) m = 
            match m with 
            | MapEmpty -> true
            | MapOne(k2,v2) -> f.Invoke(k2, v2)
            | MapNode(k2,v2,l,r,_,_) -> forallOpt f l && f.Invoke(k2, v2) && forallOpt f r

        let forall f m = forallOpt (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m

        let rec map f m = 
            match m with 
            | MapEmpty -> empty
            | MapOne(k,v) -> MapOne(k,f v)
            | MapNode(k,v,l,r,h,c) -> 
                let l2 = map f l 
                let v2 = f v 
                let r2 = map f r 
                MapNode(k,v2,l2, r2,h,c)

        let rec mapiOpt (f:OptimizedClosures.FSharpFunc<_,_,_>) m = 
            match m with
            | MapEmpty -> empty
            | MapOne(k,v) -> MapOne(k, f.Invoke(k, v))
            | MapNode(k,v,l,r,h,c) -> 
                let l2 = mapiOpt f l 
                let v2 = f.Invoke(k, v) 
                let r2 = mapiOpt f r 
                MapNode(k,v2, l2, r2,h,c)

        let mapi f m = mapiOpt (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m

        let rec mapiMonotonicAux (f:OptimizedClosures.FSharpFunc<_,_,_>) m =
            match m with
            | MapEmpty -> empty
            | MapOne(k,v) -> 
                let (k2, v2) = f.Invoke(k, v)
                MapOne(k2, v2)
            | MapNode(k,v,l,r,h,c) -> 
                let l2 = mapiMonotonicAux f l 
                let k2, v2 = f.Invoke(k, v) 
                let r2 = mapiMonotonicAux f r 
                MapNode(k2,v2, l2, r2,h,c)

        let mapiMonotonic f m = mapiMonotonicAux (OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)) m
    
        let rec tryMinAux acc m =
            match m with
            | MapEmpty -> acc
            | MapOne(k,_) -> Some k
            | MapNode(k,_,l,_,_,_) -> tryMinAux (Some k) l

        let rec tryMin m = tryMinAux None m
            
    
        let rec tryMaxAux acc m =
            match m with
            | MapEmpty -> acc
            | MapOne(k,_) -> Some k
            | MapNode(k,_,_,r,_,_) -> tryMaxAux (Some k) r

        let rec tryMax m = tryMaxAux None m
        
    
        let rec tryAt i m =
            match m with
            | MapEmpty -> 
                None

            | MapOne(k,v) -> 
                if i = 0 then Some (k,v)
                else None

            | MapNode(k,v,l,r,_,c) ->
                if i < 0 || i >= c then
                    None
                else
                    let ls = size l
                    if i = ls then
                        Some (k,v)
                    elif i < ls then
                        tryAt i l
                    else
                        tryAt (i - ls - 1) r

                                
            
        let rec foldBackOpt (f:OptimizedClosures.FSharpFunc<_,_,_,_>) m x = 
            match m with 
            | MapEmpty -> x
            | MapOne(k,v) -> f.Invoke(k,v,x)
            | MapNode(k,v,l,r,_,_) -> 
                let x = foldBackOpt f r x
                let x = f.Invoke(k,v,x)
                foldBackOpt f l x

        let foldBack f m x = foldBackOpt (OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)) m x

        let rec foldOpt (f:OptimizedClosures.FSharpFunc<_,_,_,_>) x m  = 
            match m with 
            | MapEmpty -> x
            | MapOne(k,v) -> f.Invoke(x,k,v)
            | MapNode(k,v,l,r,_,_) -> 
                let x = foldOpt f x l
                let x = f.Invoke(x,k,v)
                foldOpt f x r

        let fold f x m = foldOpt (OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)) x m

        let foldSectionOpt (comparer: IComparer<'Value>) lo hi (f:OptimizedClosures.FSharpFunc<_,_,_,_>) m x =
            let rec foldFromTo (f:OptimizedClosures.FSharpFunc<_,_,_,_>) m x = 
                match m with 
                | MapEmpty -> x
                | MapOne(k,v) ->
                    let cLoKey = comparer.Compare(lo,k)
                    let cKeyHi = comparer.Compare(k,hi)
                    let x = if cLoKey <= 0 && cKeyHi <= 0 then f.Invoke(k, v, x) else x
                    x
                | MapNode(k,v,l,r,_,_) ->
                    let cLoKey = comparer.Compare(lo,k)
                    let cKeyHi = comparer.Compare(k,hi)
                    let x = if cLoKey < 0                 then foldFromTo f l x else x
                    let x = if cLoKey <= 0 && cKeyHi <= 0 then f.Invoke(k, v, x) else x
                    let x = if cKeyHi < 0                 then foldFromTo f r x else x
                    x
           
            if comparer.Compare(lo,hi) = 1 then x else foldFromTo f m x

        let foldSection (comparer: IComparer<'Value>) lo hi f m x =
            foldSectionOpt comparer lo hi (OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)) m x

        let toList m = 
            let rec loop m acc = 
                match m with 
                | MapEmpty -> acc
                | MapOne(k,v) -> (k,v)::acc
                | MapNode(k,v,l,r,_,_) -> loop l ((k,v)::loop r acc)
            loop m []
        let toArray m = m |> toList |> Array.ofList
        let ofList comparer l = List.fold (fun acc (k,v) -> add comparer k v acc) empty l

        let rec mkFromEnumerator comparer acc (e : IEnumerator<_>) = 
            if e.MoveNext() then 
                let (x,y) = e.Current 
                mkFromEnumerator comparer (add comparer x y acc) e
            else acc
          
        let ofArray comparer (arr : array<_>) =
            let mutable res = empty
            for (x,y) in arr do
                res <- add comparer x y res 
            res

        let ofSeq comparer (c : seq<'Key * 'T>) =
            match c with 
            | :? array<'Key * 'T> as xs -> ofArray comparer xs
            | :? list<'Key * 'T> as xs -> ofList comparer xs
            | _ -> 
                use ie = c.GetEnumerator()
                mkFromEnumerator comparer empty ie 

          
        let copyToArray s (arr: _[]) i =
            let j = ref i 
            s |> iter (fun x y -> arr.[!j] <- KeyValuePair(x,y); j := !j + 1)


        /// Imperative left-to-right iterators.
        [<NoEquality; NoComparison>]
        type MapIterator<'Key,'Value when 'Key : comparison > = 
                { /// invariant: always collapseLHS result 
                mutable stack: MapTree<'Key,'Value> list;  
                /// true when MoveNext has been called   
                mutable started : bool }

        // collapseLHS:
        // a) Always returns either [] or a list starting with MapOne.
        // b) The "fringe" of the set stack is unchanged. 
        let rec collapseLHS stack =
            match stack with
            | []                           -> []
            | MapEmpty             :: rest -> collapseLHS rest
            | MapOne _         :: _ -> stack
            | (MapNode(k,v,l,r,_,_)) :: rest -> collapseLHS (l :: MapOne (k,v) :: r :: rest)
          
        let mkIterator s = { stack = collapseLHS [s]; started = false }

        let notStarted() = raise (InvalidOperationException("enumeration not started"))
        let alreadyFinished() = raise (InvalidOperationException("enumeration finished"))

        let current i =
            if i.started then
                match i.stack with
                    | MapOne (k,v) :: _ -> new KeyValuePair<_,_>(k,v)
                    | []            -> alreadyFinished()
                    | _             -> failwith "Please report error: MapExt iterator, unexpected stack for current"
            else
                notStarted()

        let rec moveNext i =
            if i.started then
                match i.stack with
                    | MapOne _ :: rest -> 
                        i.stack <- collapseLHS rest
                        not i.stack.IsEmpty
                    | [] -> false
                    | _ -> failwith "Please report error: MapExt iterator, unexpected stack for moveNext"
            else
                i.started <- true  (* The first call to MoveNext "starts" the enumeration. *)
                not i.stack.IsEmpty

        let mkIEnumerator s = 
            let i = ref (mkIterator s) 
            { new IEnumerator<_> with 
                member __.Current = current !i
            interface System.Collections.IEnumerator with
                member __.Current = box (current !i)
                member __.MoveNext() = moveNext !i
                member __.Reset() = i :=  mkIterator s
            interface System.IDisposable with 
                member __.Dispose() = ()}


open MapExtImplementation

[<System.Diagnostics.DebuggerTypeProxy(typedefof<MapDebugView<_,_>>)>]
[<System.Diagnostics.DebuggerDisplay("Count = {Count}")>]
[<Sealed>]
type MapExt<[<EqualityConditionalOn>]'Key,[<EqualityConditionalOn;ComparisonConditionalOn>]'Value when 'Key : comparison >(comparer: IComparer<'Key>, tree: MapTree<'Key,'Value>) =

    // We use .NET generics per-instantiation static fields to avoid allocating a new object for each empty
    // set (it is just a lookup into a .NET table of type-instantiation-indexed static fields).
    static let empty = 
        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
        new MapExt<'Key,'Value>(comparer,MapTree<_,_>.MapEmpty)

    static member Empty : MapExt<'Key,'Value> = empty

    static member Create(ie : IEnumerable<_>) : MapExt<'Key,'Value> = 
        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
        new MapExt<_,_>(comparer,MapTree.ofSeq comparer ie)
    
    static member Create() : MapExt<'Key,'Value> = empty

    new(ie : seq<_>) = 
        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
        new MapExt<_,_>(comparer,MapTree.ofSeq comparer ie)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member internal m.Comparer = comparer
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member internal m.Tree = tree
    member m.Add(k,v) : MapExt<'Key,'Value> = 
        new MapExt<'Key,'Value>(comparer,MapTree.add comparer k v tree)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member m.IsEmpty = MapTree.isEmpty tree
    member m.Item 
        with get(k : 'Key) = MapTree.find comparer k tree

    member x.TryAt i = MapTree.tryAt i tree

    member m.TryPick(f) = MapTree.tryPick f tree 
    member m.Exists(f) = MapTree.exists f tree 
    member m.Filter(f)  : MapExt<'Key,'Value> = new MapExt<'Key,'Value>(comparer ,MapTree.filter comparer f tree)
    member m.ForAll(f) = MapTree.forall f tree 
    member m.Fold f acc = MapTree.foldBack f tree acc

    member m.FoldSection (lo:'Key) (hi:'Key) f (acc:'z) = MapTree.foldSection comparer lo hi f tree acc 

    member m.Iterate f = MapTree.iter f tree

    member m.MapRange f  = new MapExt<'Key,'b>(comparer,MapTree.map f tree)

    member m.MapExt f  = new MapExt<'Key,'b>(comparer,MapTree.mapi f tree)
    
    member m.MapMonotonic f  = new MapExt<'Key2,'Value2>(LanguagePrimitives.FastGenericComparer<'Key2>, MapTree.mapiMonotonic f tree)

    member m.Alter(k, f) = new MapExt<'Key, 'Value>(comparer, MapTree.alter comparer k f tree)

    member m.Partition(f)  : MapExt<'Key,'Value> * MapExt<'Key,'Value> = 
        let r1,r2 = MapTree.partition comparer f tree  in 
        new MapExt<'Key,'Value>(comparer,r1), new MapExt<'Key,'Value>(comparer,r2)

    member m.Count = MapTree.size tree

    member x.TryMinKey = MapTree.tryMin tree
    member x.TryMaxKey = MapTree.tryMax tree

    member m.ContainsKey(k) = 
        MapTree.mem comparer k tree

    member m.Remove(k)  : MapExt<'Key,'Value> = 
        new MapExt<'Key,'Value>(comparer,MapTree.remove comparer k tree)

    member m.TryFind(k) = 
        MapTree.tryFind comparer k tree

    member m.ToList() = MapTree.toList tree

    member m.ToArray() = MapTree.toArray tree

    static member ofList(l) : MapExt<'Key,'Value> = 
        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
        new MapExt<_,_>(comparer,MapTree.ofList comparer l)
           
    member this.ComputeHashCode() = 
        let combineHash x y = (x <<< 1) + y + 631 
        let mutable res = 0
        for (KeyValue(x,y)) in this do
            res <- combineHash res (hash x)
            res <- combineHash res (Unchecked.hash y)
        abs res

    override this.Equals(that) = 
        match that with 
        | :? MapExt<'Key,'Value> as that -> 
            use e1 = (this :> seq<_>).GetEnumerator() 
            use e2 = (that :> seq<_>).GetEnumerator() 
            let rec loop () = 
                let m1 = e1.MoveNext() 
                let m2 = e2.MoveNext()
                (m1 = m2) && (not m1 || let e1c, e2c = e1.Current, e2.Current in ((e1c.Key = e2c.Key) && (Unchecked.equals e1c.Value e2c.Value) && loop()))
            loop()
        | _ -> false

    override this.GetHashCode() = this.ComputeHashCode()

    interface IEnumerable<KeyValuePair<'Key, 'Value>> with
        member __.GetEnumerator() = MapTree.mkIEnumerator tree

    interface System.Collections.IEnumerable with
        member __.GetEnumerator() = (MapTree.mkIEnumerator tree :> System.Collections.IEnumerator)

    interface IDictionary<'Key, 'Value> with 
        member m.Item 
            with get x = m.[x]            
            and  set x v = ignore(x,v); raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)"))

        // REVIEW: this implementation could avoid copying the Values to an array    
        member s.Keys = ([| for kvp in s -> kvp.Key |] :> ICollection<'Key>)

        // REVIEW: this implementation could avoid copying the Values to an array    
        member s.Values = ([| for kvp in s -> kvp.Value |] :> ICollection<'Value>)

        member s.Add(k,v) = ignore(k,v); raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)"))
        member s.ContainsKey(k) = s.ContainsKey(k)
        member s.TryGetValue(k,r) = if s.ContainsKey(k) then (r <- s.[k]; true) else false
        member s.Remove(k : 'Key) = ignore(k); (raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)")) : bool)

    interface ICollection<KeyValuePair<'Key, 'Value>> with 
        member __.Add(x) = ignore(x); raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)"));
        member __.Clear() = raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)"));
        member __.Remove(x) = ignore(x); raise (NotSupportedException("SR.GetString(SR.mapCannotBeMutated)"));
        member s.Contains(x) = s.ContainsKey(x.Key) && Unchecked.equals s.[x.Key] x.Value
        member __.CopyTo(arr,i) = MapTree.copyToArray tree arr i
        member s.IsReadOnly = true
        member s.Count = s.Count

    interface System.IComparable with 
        member m.CompareTo(obj: obj) = 
            match obj with 
            | :? MapExt<'Key,'Value>  as m2->
                Seq.compareWith 
                    (fun (kvp1 : KeyValuePair<_,_>) (kvp2 : KeyValuePair<_,_>)-> 
                        let c = comparer.Compare(kvp1.Key,kvp2.Key) in 
                        if c <> 0 then c else Unchecked.compare kvp1.Value kvp2.Value)
                    m m2 
            | _ -> 
                invalidArg "obj" ("SR.GetString(SR.notComparable)")

    override x.ToString() = 
        match List.ofSeq (Seq.truncate 4 x) with 
        | [] -> "map []"
        | [KeyValue h1] -> System.Text.StringBuilder().Append("map [").Append(string h1).Append("]").ToString()
        | [KeyValue h1;KeyValue h2] -> System.Text.StringBuilder().Append("map [").Append(string h1).Append("; ").Append(string h2).Append("]").ToString()
        | [KeyValue h1;KeyValue h2;KeyValue h3] -> System.Text.StringBuilder().Append("map [").Append(string h1).Append("; ").Append(string h2).Append("; ").Append(string h3).Append("]").ToString()
        | KeyValue h1 :: KeyValue h2 :: KeyValue h3 :: _ -> System.Text.StringBuilder().Append("map [").Append(string h1).Append("; ").Append(string h2).Append("; ").Append(string h3).Append("; ... ]").ToString() 

and 
    [<Sealed>]
    MapDebugView<'Key,'Value when 'Key : comparison>(v: MapExt<'Key,'Value>)  =  

        [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
        member x.Items = v |> Seq.truncate 10000 |> Seq.toArray


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module MapExt = 

    [<CompiledName("IsEmpty")>]
    let isEmpty (m:MapExt<_,_>) = m.IsEmpty

    [<CompiledName("Add")>]
    let add k v (m:MapExt<_,_>) = m.Add(k,v)

    [<CompiledName("Find")>]
    let find k (m:MapExt<_,_>) = m.[k]

    [<CompiledName("TryFind")>]
    let tryFind k (m:MapExt<_,_>) = m.TryFind(k)

    [<CompiledName("Remove")>]
    let remove k (m:MapExt<_,_>) = m.Remove(k)

    [<CompiledName("ContainsKey")>]
    let containsKey k (m:MapExt<_,_>) = m.ContainsKey(k)

    [<CompiledName("Iterate")>]
    let iter f (m:MapExt<_,_>) = m.Iterate(f)

    [<CompiledName("TryPick")>]
    let tryPick f (m:MapExt<_,_>) = m.TryPick(f)

    [<CompiledName("Pick")>]
    let pick f (m:MapExt<_,_>) = match tryPick f m with None -> raise (KeyNotFoundException()) | Some res -> res

    [<CompiledName("Exists")>]
    let exists f (m:MapExt<_,_>) = m.Exists(f)

    [<CompiledName("Filter")>]
    let filter f (m:MapExt<_,_>) = m.Filter(f)

    [<CompiledName("Partition")>]
    let partition f (m:MapExt<_,_>) = m.Partition(f)

    [<CompiledName("ForAll")>]
    let forall f (m:MapExt<_,_>) = m.ForAll(f)

    let mapRange f (m:MapExt<_,_>) = m.MapRange(f)

    [<CompiledName("MapExt")>]
    let map f (m:MapExt<_,_>) = m.MapExt(f)

    [<CompiledName("Fold")>]
    let fold<'Key,'T,'State when 'Key : comparison> f (z:'State) (m:MapExt<'Key,'T>) = MapTree.fold f z m.Tree

    [<CompiledName("FoldBack")>]
    let foldBack<'Key,'T,'State  when 'Key : comparison> f (m:MapExt<'Key,'T>) (z:'State) =  MapTree.foldBack  f m.Tree z
        
    [<CompiledName("ToSeq")>]
    let toSeq (m:MapExt<_,_>) = m |> Seq.map (fun kvp -> kvp.Key, kvp.Value)

    [<CompiledName("FindKey")>]
    let findKey f (m : MapExt<_,_>) = m |> toSeq |> Seq.pick (fun (k,v) -> if f k v then Some(k) else None)

    [<CompiledName("TryFindKey")>]
    let tryFindKey f (m : MapExt<_,_>) = m |> toSeq |> Seq.tryPick (fun (k,v) -> if f k v then Some(k) else None)

    [<CompiledName("OfList")>]
    let ofList (l: ('Key * 'Value) list) = MapExt<_,_>.ofList(l)

    [<CompiledName("OfSeq")>]
    let ofSeq l = MapExt<_,_>.Create(l)

    [<CompiledName("OfArray")>]
    let ofArray (array: ('Key * 'Value) array) = 
        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
        new MapExt<_,_>(comparer,MapTree.ofArray comparer array)

    [<CompiledName("ToList")>]
    let toList (m:MapExt<_,_>) = m.ToList()

    [<CompiledName("ToArray")>]
    let toArray (m:MapExt<_,_>) = m.ToArray()

    [<CompiledName("Empty")>]
    let empty<'Key,'Value  when 'Key : comparison> = MapExt<'Key,'Value>.Empty

    [<CompiledName("Count")>]
    let count (m:MapExt<_,_>) = m.Count
    
    [<CompiledName("TryMin")>]
    let tryMin (m:MapExt<_,_>) = m.TryMinKey
    
    [<CompiledName("Min")>]
    let min (m:MapExt<_,_>) = 
        match m.TryMinKey with
            | Some min -> min
            | None -> raise <| ArgumentException("The input sequence was empty.")

    [<CompiledName("TryMax")>]
    let tryMax (m:MapExt<_,_>) = m.TryMaxKey

    [<CompiledName("Max")>]
    let max (m:MapExt<_,_>) = 
        match m.TryMaxKey with
            | Some min -> min
            | None -> raise <| ArgumentException("The input sequence was empty.")

    
    [<CompiledName("TryItem")>]
    let tryItem i (m:MapExt<_,_>) = m.TryAt i

    [<CompiledName("TryItem")>]
    let item i (m:MapExt<_,_>) = 
        match m.TryAt i with
            | Some t -> t
            | None -> raise <| IndexOutOfRangeException()

    [<CompiledName("Alter")>]
    let alter k f (m:MapExt<_,_>) = m.Alter(k, f)
    
    [<CompiledName("MapMonotonic")>]
    let mapMonotonic f (m:MapExt<_,_>) = m.MapMonotonic(f)
