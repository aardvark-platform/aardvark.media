namespace Aardvark.UI

open System
open FSharp.Data.Adaptive

[<AbstractClass>]
type SceneEventProcessor<'msg>() =

    static let empty =
        { new SceneEventProcessor<'msg>() with
            member x.NeededEvents = ASet.empty
            member x.Process(_,_) = Seq.empty
        }

    static member Empty = empty

    abstract member NeededEvents : aset<SceneEventKind>
    abstract member Process : source : Guid * evt : SceneEvent -> seq<'msg>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SceneEventProcessor =

    [<AutoOpen>]
    module private Implementation =
        type UnionProcessor<'msg>(inner : list<SceneEventProcessor<'msg>>) =
            inherit SceneEventProcessor<'msg>()

            let needed =
                lazy (
                    inner |> List.map _.NeededEvents |> ASet.ofList |> ASet.unionMany
                )

            override x.NeededEvents = needed.Value
            override x.Process(sender, e) =
                seq {
                    for p in inner do
                        yield! p.Process(sender, e)
                }

    let empty<'msg> = SceneEventProcessor<'msg>.Empty

    let union (l : SceneEventProcessor<'msg>) (r : SceneEventProcessor<'msg>) =
        UnionProcessor [l;r] :> SceneEventProcessor<'msg>

    let unionMany (processors : seq<SceneEventProcessor<'msg>>) =
        processors |> Seq.toList |> UnionProcessor :> SceneEventProcessor<'msg>