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

    let mutable last = None
    let entered = System.Collections.Generic.HashSet<_>()

    static let intersectLeaf (_: SceneEventKind) (part: RayPart) (obj: PickObject) =
        let pickable = obj.Pickable |> AVal.force
        match Pickable.intersect part pickable with
        | Some t ->
            match obj.Scope.TryGetInheritedV "PickProcessor" with
            | ValueSome (:? ISceneHitProcessor<'msg> as proc) -> Some <| RayHit(t, proc)
            | _ -> None
        | None ->
            None

    member private x.Perform(evt: SceneEvent, bvh: BvhTree<PickObject>) =
        let intersections = bvh.Intersections(intersectLeaf evt.kind, evt.globalRay)
        use e = intersections.GetEnumerator()

        let rec run (evt: SceneEvent) (seen: HashSet<ISceneHitProcessor<'msg>>) (contEnter: bool) =
            //let topLevel = HSet.isEmpty seen

            if e.MoveNext() then
                let hit = e.Current
                let proc = hit.Value

                if HashSet.contains proc seen then
                    run evt seen contEnter
                else
                    let cont, msgs =
                        proc.Process { event = evt; rayT = hit.T }

                    // rethink this stuff

                    let cc, msgs =
                        if Some proc <> last && contEnter then
                            let l = last
                            let cc,enters = proc.Process { event = { evt with evtKind = SceneEventKind.Enter }; rayT = hit.T }
                            entered.Add proc |> ignore
                            if not cc then
                                //last <- Some proc
                                false, seq {
                                    match l with
                                    | Some l ->
                                        let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = hit.T }
                                        yield! leaves
                                    | None ->
                                        ()

                                    yield! enters
                                    yield! msgs
                                }
                            else
                                //last <- Some proc
                                true, msgs
                        else
                            entered.Add proc |> ignore
                            true, msgs

                    if cont then
                        let consumed, rest = run evt (HashSet.add proc seen) cc
                        consumed, Seq.append msgs rest
                    else
                        true, msgs

            else
                match last with
                | Some l when contEnter ->
                    if entered.Contains l then false, Seq.empty
                    else
                        last <- None
                        let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 }
                        false, leaves
                | _ ->
                    false, Seq.empty

        let oldEntered = entered |> Aardvark.Base.HashSet.toList
        entered.Clear()

        let c, msgs = run evt HashSet.empty true

        let leaves =
            seq {
                for o in oldEntered do
                    if entered.Contains o then ()
                    else
                        let _,msgs =  o.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 }
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