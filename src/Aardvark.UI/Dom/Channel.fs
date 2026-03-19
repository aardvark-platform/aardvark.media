namespace Aardvark.UI

open System
open FSharp.Data.Adaptive

[<AbstractClass>]
type ChannelReader() =
    inherit AdaptiveObject()

    abstract member ComputeMessages : AdaptiveToken -> list<string>
    abstract member Release : unit -> unit

    member x.GetMessages(t : AdaptiveToken) =
        x.EvaluateIfNeeded t [] x.ComputeMessages

    member x.Dispose() =
        x.Release()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass>]
type Channel() =
    abstract member GetReader : unit -> ChannelReader

[<AutoOpen>]
module ``Channel Extensions`` =
    type private ModChannelReader<'a>(m : aval<'a>) =
        inherit ChannelReader()

        let mutable last = None

        override x.Release() =
            last <- None

        override x.ComputeMessages t =
            let v = m.GetValue t

            if Unchecked.equals last (Some v) then
                []
            else
                last <- Some v
                [ Pickler.json.PickleToString v ]

    type private ASetChannelReader<'a>(s : aset<'a>) =
        inherit ChannelReader()

        let mutable reader = s.GetReader()

        override x.Release() =
            reader <- Unchecked.defaultof<_>

        override x.ComputeMessages t =
            let ops = reader.GetChanges t
            ops |> HashSetDelta.toList |> List.map Pickler.json.PickleToString

    type private AValChannel<'a>(m : aval<'a>) =
        inherit Channel()
        override x.GetReader() = new ModChannelReader<_>(m) :> ChannelReader

    type private ASetChannel<'a>(m : aset<'a>) =
        inherit Channel()
        override x.GetReader() = new ASetChannelReader<_>(m) :> ChannelReader

    type IAdaptiveValue<'a> with
        member x.Channel = AValChannel(x) :> Channel

    type IAdaptiveHashSet<'a> with
        member x.Channel = ASetChannel(x) :> Channel

    module AVal =
        let inline channel (m : aval<'a>) = m.Channel

    module ASet =
        let inline channel (m : aset<'a>) = m.Channel