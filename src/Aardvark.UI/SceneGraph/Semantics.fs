namespace Aardvark.UI.Semantics

open Aardvark.UI
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Base.Ag

[<AutoOpen>]
module ``Sg Semantics`` =
    open Aardvark.SceneGraph.Semantics
    open Aardvark.UI.Sg
    open Aardvark.UI.MessageProcessor.Implementation

    type GlobalPicks<'msg> = amap<SceneEventKind, SceneEvent -> seq<'msg>>

    type ISg<'msg> with
        member this.RenderObjects(scope: Scope)     : aset<IRenderObject> = this?RenderObjects(scope)
        member this.PickObjects(scope: Scope)       : aset<PickObject>    = this?PickObjects(scope)
        member this.GlobalPicks(scope: Scope)       : GlobalPicks<'msg>   = this?GlobalPicks(scope)
        member this.GlobalBoundingBox(scope: Scope) : aval<Box3d>         = this?GlobalBoundingBox(scope)
        member this.LocalBoundingBox(scope: Scope)  : aval<Box3d>         = this?LocalBoundingBox(scope)

    type Scope with
        member this.MessageProcessor() : IMessageProcessor<'msg> = this?MessageProcessor
        member this.PickProcessor      : ISceneHitProcessor      = this?PickProcessor

    let rec collectMsgSgs (mapping: ISg<'msg> -> aset<'a>) (sg: ISg) : aset<'a> =
        match sg with
        | :? ISg<'msg> as sg ->
            mapping sg

        | :? IApplicator as a ->
            a.Child |> ASet.bind (collectMsgSgs mapping)

        | :? IGroup as g ->
            g.Children |> ASet.collect (collectMsgSgs mapping)

        | _ ->
            ASet.empty

    [<Rule>]
    type StandardSems() =
        member x.RenderObjects(g: IGroup<'msg>, scope: Scope) : aset<IRenderObject> =
            g.Children |> ASet.collect _.RenderObjects(scope)

        member x.PickObjects(g: IGroup<'msg>, scope: Scope) : aset<PickObject> =
            g.Children |> ASet.collect _.PickObjects(scope)

        member x.GlobalBoundingBox(g: IGroup<'msg>, scope: Scope) =
            let add (l: Box3d) (r: Box3d) =
                Box3d(l, r)

            let trySub (s: Box3d) (b: Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children
            |> ASet.mapA _.GlobalBoundingBox(scope)
            |> ASet.foldHalfGroup add trySub Box3d.Invalid

        member x.LocalBoundingBox(g: IGroup<'msg>, scope: Scope) =
            let add (l: Box3d) (r: Box3d) =
                Box3d(l, r)

            let trySub (s: Box3d) (b: Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children
            |> ASet.mapA _.LocalBoundingBox(scope)
            |> ASet.foldHalfGroup add trySub Box3d.Invalid

        member x.GlobalPicks(g: IGroup<'msg>, scope: Scope) : GlobalPicks<'msg> =
            g.Children
            |> ASet.collect (fun g -> g.GlobalPicks(scope) |> AMap.toASet)
            |> AMap.ofASet
            |> AMap.map (fun _ vs e ->
                seq {
                    for v in vs do
                        yield! v e
                }
            )

        member x.GlobalPicks(a: IApplicator<'msg>, scope: Scope) : GlobalPicks<'msg> =
            a.Child.GlobalPicks(scope)

        member x.GlobalPicks(a: Adapter<'msg>, scope: Scope) : GlobalPicks<'msg> =
            a.Child
            |> ASet.bind (collectMsgSgs (fun g -> g.GlobalPicks(scope) |> AMap.toASet))
            |> AMap.ofASet
            |> AMap.map (fun _ vs e ->
                seq {
                    for v in vs do
                        yield! v e
                }
            )

        member x.GlobalPicks(g: GlobalEvent<'msg>, scope: Scope) : GlobalPicks<'msg> =
            let b = g.Child.GlobalPicks(scope)
            let trafo = scope.ModelTrafo
            let own = g.Events |> AMap.map (fun _ l e -> l { e with evtTrafo = trafo })
            AMap.unionWith (fun _ l r -> fun e -> Seq.append (l e) (r e)) own b

        member x.GlobalPicks(_: ISg<'msg>, _: Scope) : GlobalPicks<'msg>  =
            AMap.empty

        member x.GlobalPicks(ma: MapApplicator<'inner, 'outer>, scope: Scope) : GlobalPicks<'outer> =
            let picks = ma.Child.GlobalPicks(scope)
            picks |> AMap.map (fun _ v e -> v e |> Seq.collect ma.Mapping)

    [<Rule>]
    type MessageProcessorSem() =

        member x.MessageProcessor(root: Root<ISg<'msg>>, _: Scope) =
            root.Child?MessageProcessor <- MessageProcessor.id<'msg>

        member x.MessageProcessor(app: MapApplicator<'inner, 'outer>, scope: Scope) =
            let parent = scope.MessageProcessor()
            app.Child?MessageProcessor <- parent.Map(ASet.empty, app.Mapping)

        member x.PickProcessor(root: Root<ISg<'msg>>, _: Scope) =
            root.Child?PickProcessor <- IgnoreProcessor<obj, 'msg>.Instance :> ISceneHitProcessor

        member x.PickProcessor(app: EventApplicator<'msg>, scope: Scope) =
            let msg = scope.MessageProcessor()

            let needed =
                app.Events |> AMap.keys |> ASet.map (fun n ->
                    match n with
                    | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                    | _ -> n
                )

            let trafo = scope.ModelTrafo

            let processor (hit: SceneHit) =
                let evts = app.Events.Content |> AVal.force

                let createArtificialMove =
                    (HashMap.containsKey SceneEventKind.Enter evts || HashMap.containsKey SceneEventKind.Leave evts) &&
                    (not <| HashMap.containsKey SceneEventKind.Move evts)

                let evts =
                    if createArtificialMove then HashMap.add SceneEventKind.Move (fun _ -> true, Seq.empty) evts
                    else evts

                match HashMap.tryFindV hit.kind evts with
                | ValueSome cb -> cb { hit with event.evtTrafo = trafo }
                | ValueNone -> true, Seq.empty

            app.Child?PickProcessor <- msg.MapHit(needed, processor)