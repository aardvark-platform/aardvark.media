namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Geometry
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive

type PickTree<'msg>(sg: ISg<'msg>) =
    let objects : aset<PickObject> = sg.PickObjects(Scope.Root)
    let bvh = BvhTree.ofASet PickObject.bounds objects

    let needed =
        objects |> ASet.collect (fun o ->
            match o.Scope.TryGetInheritedV "PickProcessor" with
            | ValueSome (:? ISceneHitProcessor<'msg> as proc) -> proc.NeededEvents
            | _ -> ASet.empty
        )

    let mutable entered = HashMap.empty
    let mutable enterOrder = []

    static let intersectLeaf (_: SceneEventKind) (part: RayPart) (obj: PickObject) =
        let pickable = obj.Pickable |> AVal.force
        match Pickable.intersectV part pickable with
        | ValueSome t ->
            match obj.Scope.TryGetInheritedV "PickProcessor" with
            | ValueSome (:? ISceneHitProcessor<'msg> as proc) -> ValueSome <| RayHit(t, proc)
            | _ -> ValueNone
        | _ ->
            ValueNone

    member private x.Perform(evt: SceneEvent, bvh: BvhTree<PickObject>) =
        let intersections = bvh.IntersectionsV(intersectLeaf evt.kind, evt.globalRay)
        use e = intersections.GetEnumerator()

        let oldEntered = entered
        let oldOrder = enterOrder
        entered <- HashMap.empty
        enterOrder <- []

        let rec run (evt: SceneEvent) (seen: HashSet<ISceneHitProcessor<'msg>>) (contEnter: bool) =
            if e.MoveNext() then
                let hit = e.Current
                let proc = hit.Value

                if HashSet.contains proc seen then
                    run evt seen contEnter
                else
                    let cont, msgs =
                        proc.Process { event = evt; rayT = hit.T }

                    let cc, msgs =
                        if contEnter then
                            let cc, msgs =
                                match HashMap.tryFindV proc oldEntered with
                                | ValueSome cc -> cc, msgs
                                | ValueNone ->
                                    let cc, enters = proc.Process { event = { evt with evtKind = SceneEventKind.Enter }; rayT = hit.T }
                                    cc, seq { yield! enters; yield! msgs }
                            entered <- HashMap.add proc cc entered
                            enterOrder <- proc :: enterOrder
                            cc, msgs
                        else
                            false, msgs

                    if cont then
                        let consumed, rest = run evt (HashSet.add proc seen) cc
                        consumed, Seq.append msgs rest
                    else
                        true, msgs
            else
                false, Seq.empty

        let c, msgs = run evt HashSet.empty true

        let leaves =
            seq {
                for o in oldOrder do
                    if HashMap.containsKey o entered then ()
                    else
                        let _, msgs = o.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 }
                        yield! msgs
            }
        c, seq { yield! leaves; yield! msgs }

    member x.Needed = needed

    member x.Perform(evt : SceneEvent) =
        let bvh = bvh |> AVal.force
        x.Perform(evt, bvh)

module PickTree =
    let ofSg (sg: ISg<'msg>) = PickTree<'msg>(sg)
    let perform (evt: SceneEvent) (tree: PickTree<'msg>) = tree.Perform(evt)