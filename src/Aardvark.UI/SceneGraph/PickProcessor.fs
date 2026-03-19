namespace Aardvark.UI

open FSharp.Data.Adaptive

type ISceneHitProcessor =
    abstract member NeededEvents : aset<SceneEventKind>

type ISceneHitProcessor<'a> =
    inherit ISceneHitProcessor
    abstract member Process : SceneHit -> bool * seq<'a>

type IMessageProcessor<'a> =
    abstract member NeededEvents : aset<SceneEventKind>
    abstract member Map : aset<SceneEventKind> * ('x -> seq<'a>) -> IMessageProcessor<'x>
    abstract member MapHit : aset<SceneEventKind> * (SceneHit -> bool * seq<'a>) -> ISceneHitProcessor

type IMessageProcessor<'a, 'b> =
    inherit IMessageProcessor<'a>
    abstract member Process : 'a -> seq<'b>

module MessageProcessor =

    [<AutoOpen>]
    module Implementation =

        type HitProcessor<'a>(needed : aset<SceneEventKind>, mapping : SceneHit -> bool * seq<'a>) =
            member x.Process(msg : SceneHit) =
                mapping msg

            interface ISceneHitProcessor with
                member x.NeededEvents = needed

            interface ISceneHitProcessor<'a> with
                member x.Process hit = x.Process hit

        type Processor<'a, 'b>(needed : aset<SceneEventKind>, mapping : 'a -> seq<'b>) =
            member x.Map(newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) =
                Processor<'x, 'b>(ASet.union needed newNeeded, f >> Seq.collect mapping) :> IMessageProcessor<'x, 'b>

            member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                let f x =
                    let cont, msgs = f x
                    cont, Seq.collect mapping msgs

                HitProcessor<'b>(ASet.union needed newNeeded, f) :> ISceneHitProcessor<'b>

            member x.Process(msg : 'a) =
                mapping msg

            interface IMessageProcessor<'a> with
                member x.NeededEvents = needed
                member x.Map (newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) = x.Map(newNeeded, f) :> IMessageProcessor<'x>
                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) = x.MapHit(newNeeded, f) :> ISceneHitProcessor

            interface IMessageProcessor<'a, 'b> with
                member x.Process msg = x.Process msg

        type IdentityProcessor<'a> private() =

            static let instance = IdentityProcessor<'a>() :> IMessageProcessor<'a>

            static member Instance = instance

            interface IMessageProcessor<'a, 'a> with
                member x.NeededEvents = ASet.empty

                member x.Map(needed : aset<SceneEventKind>, f : 'x -> seq<'a>) =
                    Processor<'x, 'a>(needed, f) :> IMessageProcessor<_>

                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                    HitProcessor<'a>(newNeeded, f) :> ISceneHitProcessor

                member x.Process(msg : 'a) =
                    Seq.singleton msg

        type IgnoreProcessor<'a, 'b> private() =

            static let instance = IgnoreProcessor<'a, 'b>()

            static member Instance = instance

            interface ISceneHitProcessor<'b> with
                member x.NeededEvents = ASet.empty
                member x.Process hit = true, Seq.empty

            interface IMessageProcessor<'a, 'b> with
                member x.NeededEvents = ASet.empty

                member x.Map(newNeeded : aset<_>, f : 'x -> seq<'a>) =
                    IgnoreProcessor<'x, 'b>.Instance :> IMessageProcessor<'x>

                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                    IgnoreProcessor<obj, 'b>.Instance :> ISceneHitProcessor

                member x.Process(msg : 'a) =
                    Seq.empty


    let id<'msg> = IdentityProcessor<'msg>.Instance

    let ignore<'a, 'b> = IgnoreProcessor<'a, 'b>.Instance

    let map (newNeeded : aset<SceneEventKind>) (mapping : 'x -> 'a) (p : IMessageProcessor<'a>) =
        p.Map(newNeeded, mapping >> Seq.singleton)

    let choose (newNeeded : aset<SceneEventKind>) (mapping : 'x -> Option<'a>) (p : IMessageProcessor<'a>) =
        p.Map(newNeeded, mapping >> Option.map Seq.singleton >> Option.defaultValue Seq.empty)

    let collect (newNeeded : aset<SceneEventKind>) (mapping : 'x -> seq<'a>) (p : IMessageProcessor<'a>) =
        p.Map(newNeeded, mapping)