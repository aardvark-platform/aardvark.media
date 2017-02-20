namespace Aardvark.Base.Incremental

open System
open Aardvark.Base.Incremental
open Aardvark.Base

type ISetReader<'a> = IOpReader<prefset<'a>, deltaset<'a>>

type aset<'a> =
    abstract member IsConstant  : bool
    abstract member Content     : IMod<prefset<'a>>
    abstract member GetReader   : unit -> ISetReader<'a>

type cset<'a>(initial : seq<'a>) =
    let history = History PRefSet.traceNoRefCount
    do initial |> Seq.map Add |> DeltaSet.ofSeq |> history.Perform |> ignore

    member x.Add(v : 'a) =
        let op = DeltaSet.single (Add v)
        history.Perform op
        
    member x.Remove(v : 'a) =
        let op = DeltaSet.single (Rem v)
        history.Perform op

    member x.Contains (v : 'a) =
        PRefSet.contains v history.State

    member x.Count =
        history.State.Count

    member x.UnionWith (other : seq<'a>) =
        let op = other |> Seq.map Add |> DeltaSet.ofSeq
        history.Perform op |> ignore

    member x.ExceptWith (other : seq<'a>) =
        let op = other |> Seq.map Rem |> DeltaSet.ofSeq
        history.Perform op |> ignore

    interface aset<'a> with
        member x.IsConstant = false
        member x.Content = history :> IMod<_>
        member x.GetReader() = history.NewReader()

    new() = cset<'a>(Seq.empty)
