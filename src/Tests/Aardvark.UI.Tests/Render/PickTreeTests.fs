namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``Pick Tree Tests`` =

    // The test host is not a scene-graph entry assembly. Register the managed rules.
    let private initialized = lazy (
        Introspection.RegisterAssembly typeof<Aardvark.SceneGraph.ISg>.Assembly
        Introspection.RegisterAssembly typeof<ISg<string>>.Assembly
    )
    let private moveWords = ["move-1"; "move-2"]
    let private enterWords = ["enter-1"; "enter-2"]

    let private moveAt x =
        { evtKind = SceneEventKind.Move
          evtRay = RayPart(FastRay3d(Ray3d(V3d(x, -2.5, 0.0), V3d.OOI)), 0.0, 20.0)
          evtPixel = V2i(17, 29); evtView = Trafo3d.Identity; evtProj = Trafo3d.Identity
          evtViewport = V2i(640, 480); evtButtons = MouseButtons.Left
          evtAlt = true; evtShift = false; evtCtrl = true; evtTrafo = AVal.constant Trafo3d.Identity }

    let private movement = moveAt 4.25

    let private geometry depth events =
        initialized.Force()
        Sg.empty<string>
        |> Sg.pickable (PickShape.Box(Box3d(V3d(-1.0, -1.0, 0.0), V3d(1.0, 1.0, 1.0))))
        |> Sg.withEvents events
        |> Sg.trafo' (Trafo3d.Translation(V3d(4.0, -3.0, depth)))

    let private checkHit target depth kind (hit: SceneHit) =
        Expect.equal hit.kind kind $"{target}: event kind"
        Expect.equal hit.event.evtPixel (V2i(17, 29)) $"{target}: pixel"
        Expect.equal hit.event.evtViewport (V2i(640, 480)) $"{target}: viewport"
        Expect.equal hit.buttons MouseButtons.Left $"{target}: buttons"
        Expect.isTrue (hit.event.evtAlt && hit.event.evtCtrl && not hit.event.evtShift) $"{target}: modifiers"
        if kind = SceneEventKind.Leave then
            Expect.equal hit.rayT -1.0 $"{target}: leave sentinel"
        else
            Expect.equal hit.rayT depth $"{target}: ray distance"
            Expect.equal hit.globalPosition (V3d(4.25, -2.5, depth)) $"{target}: world hit"
            Expect.equal hit.localPosition (V3d(0.25, 0.5, 0.0)) $"{target}: transformed local hit"

    let private callback (trace: ResizeArray<string>) target depth kind continueEvent words hit =
        checkHit target depth kind hit
        trace.Add $"{target}:call:{kind}"
        continueEvent, seq {
            trace.Add $"{target}:enum:{kind}"
            for word in words do
                trace.Add $"{target}:yield:{word}"
                yield target + "/" + word
        }

    let private target trace name depth moveContinue enter =
        geometry depth [
            SceneEventKind.Move, callback trace name depth SceneEventKind.Move moveContinue moveWords
            match enter with
            | Some (continueEvent, words) ->
                SceneEventKind.Enter, callback trace name depth SceneEventKind.Enter continueEvent words
            | None -> ()
        ]

    let private verify trace (tree: Aardvark.UI.PickTree<string>) consumed calls segments =
        let actualConsumed, messages = tree.Perform movement
        Expect.equal actualConsumed consumed "Primary Move handlers determine consumption"
        Expect.equal (List.ofSeq trace) calls "Callbacks run in ray order, Move before Enter, without enumerating messages"
        let actual = Seq.toList messages
        let expected = segments |> List.collect (fun (name, _, words) -> words |> List.map (fun word -> name + "/" + word))
        Expect.equal actual expected "Deliver all Enter messages before the target's primary messages"
        let enumerations = [
            for name, kind, words in segments do
                yield $"{name}:enum:{kind}"
                for word in words do yield $"{name}:yield:{word}"
        ]
        Expect.equal (List.ofSeq trace) (calls @ enumerations) "Preserve deferred enumeration and message order"

    let private single moveContinue enterContinue =
        let trace = ResizeArray<string>()
        let tree = Aardvark.UI.PickTree<string>(target trace "near" 2.0 moveContinue (Some (enterContinue, enterWords)))
        verify trace tree (not moveContinue) ["near:call:Move"; "near:call:Enter"]
            ["near", SceneEventKind.Enter, enterWords; "near", SceneEventKind.Move, moveWords]

    let private ordered nearContinue farContinue nearEnter farEnter =
        let trace = ResizeArray<string>()
        let near = target trace "near" 2.0 nearContinue (Some (nearEnter, enterWords))
        let far = target trace "far" 5.0 farContinue (Some (farEnter, enterWords))
        // Deliberately construct the farther target first; geometry determines delivery order.
        let tree = Aardvark.UI.PickTree<string>(Sg.ofList [far; near])
        let calls = [
            "near:call:Move"; "near:call:Enter"
            if nearContinue then
                "far:call:Move"
                if nearEnter then "far:call:Enter"
        ]
        let segments = [
            "near", SceneEventKind.Enter, enterWords
            "near", SceneEventKind.Move, moveWords
            if nearContinue then
                if nearEnter then "far", SceneEventKind.Enter, enterWords
                "far", SceneEventKind.Move, moveWords
        ]
        verify trace tree (not nearContinue || not farContinue) calls segments

    let private emptyAndAbsent () =
        for entry in [None; Some (true, []); Some (false, [])] do
            for twoTargets in [false; true] do
                for moveContinue in [false; true] do
                    let trace = ResizeArray<string>()
                    let near = target trace "near" 2.0 moveContinue entry
                    let far = target trace "far" 5.0 true entry
                    let tree = Aardvark.UI.PickTree<string>(if twoTargets then Sg.ofList [far; near] else near)
                    let farVisited = twoTargets && moveContinue
                    let farEntered = farVisited && (entry |> Option.exists fst)
                    let calls = [
                        "near:call:Move"
                        if Option.isSome entry then "near:call:Enter"
                        if farVisited then
                            "far:call:Move"
                            if farEntered then "far:call:Enter"
                    ]
                    let segments = [
                        if Option.isSome entry then "near", SceneEventKind.Enter, []
                        "near", SceneEventKind.Move, moveWords
                        if farVisited then
                            if farEntered then "far", SceneEventKind.Enter, []
                            "far", SceneEventKind.Move, moveWords
                    ]
                    verify trace tree (not moveContinue) calls segments

    let private deferredFailure kind enterContinue =
        let trace = ResizeArray<string>()
        let failure = InvalidOperationException "deferred picking failure"
        let throwing hit =
            checkHit "near" 2.0 kind hit
            trace.Add $"near:call:{kind}"
            (if kind = SceneEventKind.Enter then enterContinue else true), seq {
                trace.Add $"near:enum:{kind}"
                yield "before-failure"
                trace.Add "throw"
                raise failure
            }
        let handlers =
            if kind = SceneEventKind.Enter then
                [SceneEventKind.Move, callback trace "near" 2.0 SceneEventKind.Move true moveWords
                 SceneEventKind.Enter, throwing]
            else [SceneEventKind.Move, throwing]
        let tree = Aardvark.UI.PickTree<string>(geometry 2.0 handlers)
        let consumed, messages = tree.Perform movement
        Expect.isFalse consumed "Deferred failures do not change primary continuation"
        let calls = if kind = SceneEventKind.Enter then ["near:call:Move"; "near:call:Enter"] else ["near:call:Move"]
        Expect.equal (List.ofSeq trace) calls "Do not enumerate throwing sequences in Perform"
        let delivered = ResizeArray<string>()
        let caught =
            try messages |> Seq.iter delivered.Add; None
            with error -> Some error
        Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, failure))) "Propagate the exact deferred exception"
        Expect.equal (List.ofSeq delivered) ["before-failure"] "Retain the prefix before the failure, without later messages"
        Expect.equal (List.ofSeq trace) (calls @ [$"near:enum:{kind}"; "throw"]) "Do not enumerate primary messages after Enter fails"

    let private leaveTransition () =
        for leaveContinue in [false; true] do
            let trace = ResizeArray<string>()
            let tree = Aardvark.UI.PickTree<string>(geometry 2.0 [
                SceneEventKind.Move, callback trace "near" 2.0 SceneEventKind.Move false moveWords
                SceneEventKind.Enter, callback trace "near" 2.0 SceneEventKind.Enter false enterWords
                SceneEventKind.Leave, callback trace "near" 2.0 SceneEventKind.Leave leaveContinue ["leave-1"; "leave-2"]
            ])
            verify trace tree true ["near:call:Move"; "near:call:Enter"]
                ["near", SceneEventKind.Enter, enterWords; "near", SceneEventKind.Move, moveWords]
            trace.Clear()
            let consumed, messages = tree.Perform(moveAt 50.0)
            Expect.isFalse consumed "A miss remains unconsumed regardless of Leave's continuation"
            Expect.equal trace.Count 0 "Leave transition callbacks remain deferred"
            Expect.equal (Seq.toList messages) ["near/leave-1"; "near/leave-2"] "Retain Leave transition messages"
            Expect.equal (List.ofSeq trace) ["near:call:Leave"; "near:enum:Leave"; "near:yield:leave-1"; "near:yield:leave-2"] "Retain Leave invocation and enumeration order"

    let private transitionTarget (trace: ResizeArray<string>) (hits: ResizeArray<string * SceneHit>) name originX shapes continuation =
        initialized.Force()
        let handlers = [
            for kind in [SceneEventKind.Move; SceneEventKind.Click; SceneEventKind.Enter; SceneEventKind.Leave] do
                yield kind, fun hit ->
                    trace.Add $"{name}:call:{kind}"
                    hits.Add(name, hit)
                    continuation kind, seq {
                        trace.Add $"{name}:enum:{kind}"
                        yield $"{name}/{kind}"
                    }
        ]
        shapes
        |> List.map (fun (x, depth) ->
            Sg.empty<string>
            |> Sg.pickable (PickShape.Box(Box3d(V3d(-1.0, -1.0, 0.0), V3d(1.0, 1.0, 1.0))))
            |> Sg.trafo' (Trafo3d.Translation(V3d(x, 0.0, depth))))
        |> Sg.ofList
        |> Sg.withEvents handlers
        |> Sg.trafo' (Trafo3d.Translation(V3d(originX, -3.0, 0.0)))

    let private transitionStep (trace: ResizeArray<string>) (hits: ResizeArray<string * SceneHit>) origins
                               (tree: Aardvark.UI.PickTree<string>) evt consumed eager leaves body =
        trace.Clear()
        hits.Clear()
        let label action (name, kind, _) = $"{name}:{action}:{kind}"
        let word (name, kind, _) = $"{name}/{kind}"
        let actualConsumed, messages = tree.Perform evt
        let calls = eager |> List.map (label "call")
        Expect.equal actualConsumed consumed "Only primary-event continuation determines consumption"
        Expect.equal (List.ofSeq trace) calls "Primary and new Enter callbacks are eager, messages and Leave callbacks are deferred"
        let actual = Seq.toList messages
        let actualLeaves = actual |> List.truncate (List.length leaves)
        Expect.equal actualLeaves (leaves |> List.map word) "Every exiting processor leaves once in the existing reverse-visitation order"
        Expect.equal (actual |> List.skip (List.length leaves)) (body |> List.map word) "Enter precedes primary messages without repeat entries"
        let orderedLeaves = leaves
        let enumerations = [
            for value in orderedLeaves do
                yield label "call" value
                yield label "enum" value
            for value in body do yield label "enum" value
        ]
        Expect.equal (List.ofSeq trace) (calls @ enumerations) "Preserve callback and lazy message side-effect order"
        let expectedHits = eager @ orderedLeaves
        Expect.equal hits.Count expectedHits.Length "No duplicate callbacks for shapes sharing a processor"
        for (name, hit), (expectedName, kind, depth) in List.zip (List.ofSeq hits) expectedHits do
            Expect.equal (name, hit.kind, hit.rayT) (expectedName, kind, depth) "Callback identity, event kind and closest ray distance"
            Expect.equal (hit.event.evtPixel, hit.event.evtViewport, hit.buttons) (evt.evtPixel, evt.evtViewport, evt.buttons) "Retain event metadata"
            Expect.equal (hit.event.evtAlt, hit.event.evtShift, hit.event.evtCtrl) (evt.evtAlt, evt.evtShift, evt.evtCtrl) "Retain modifiers"
            if kind <> SceneEventKind.Leave then
                let x = evt.evtRay.Ray.Ray.Origin.X
                Expect.equal hit.globalPosition (V3d(x, -2.5, depth)) "Independent world intersection"
                Expect.equal hit.localPosition (V3d(x - Map.find name origins, 0.5, depth)) "Processor-local coordinates retain the actual transform"

    [<Tests>]
    let tests =
        testList "Render.PickTree" [
            test "Single targets deliver Enter before Move without changing consumption" {
                for moveContinue in [false; true] do
                    for enterContinue in [false; true] do single moveContinue enterContinue
            }
            test "Ray-ordered targets retain messages and continuation decisions" {
                for nearContinue in [false; true] do
                    for farContinue in [false; true] do
                        for nearEnter in [false; true] do
                            for farEnter in [false; true] do ordered nearContinue farContinue nearEnter farEnter
            }
            test "Empty and absent Enter outputs retain lazy behavior" {
                emptyAndAbsent()
            }
            test "Message sequence exceptions remain deferred and preserve identity" {
                deferredFailure SceneEventKind.Enter true
                deferredFailure SceneEventKind.Enter false
                deferredFailure SceneEventKind.Move true
            }
            test "Leave transitions retain messages and lazy invocation" {
                leaveTransition()
            }
            test "Repeated Move and Click enter an active processor only once" {
                for moveContinue in [false; true] do
                    for clickContinue in [false; true] do
                        for enterContinue in [false; true] do
                            let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                            let decision = function
                                | SceneEventKind.Move -> moveContinue
                                | SceneEventKind.Click -> clickContinue
                                | _ -> enterContinue
                            let tree = Aardvark.UI.PickTree<string>(transitionTarget trace hits "A" 4.0 [0.0, 2.0] decision)
                            let step = transitionStep trace hits (Map.ofList ["A", 4.0]) tree
                            step movement (not moveContinue) ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0] []
                                ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0]
                            for kind in [SceneEventKind.Move; SceneEventKind.Click; SceneEventKind.Click; SceneEventKind.Move] do
                                step { movement with evtKind = kind } (not (decision kind)) ["A", kind, 2.0] [] ["A", kind, 2.0]
            }
            test "Exit repeated misses and reentry balance transition callbacks" {
                for enterContinue in [false; true] do
                    for leaveContinue in [false; true] do
                        let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                        let decision = function SceneEventKind.Enter -> enterContinue | SceneEventKind.Leave -> leaveContinue | _ -> true
                        let tree = Aardvark.UI.PickTree<string>(transitionTarget trace hits "A" 4.0 [0.0, 2.0] decision)
                        let step = transitionStep trace hits (Map.ofList ["A", 4.0]) tree
                        for kind in [SceneEventKind.Move; SceneEventKind.Click] do
                            step { movement with evtKind = kind } false ["A", kind, 2.0; "A", SceneEventKind.Enter, 2.0] []
                                ["A", SceneEventKind.Enter, 2.0; "A", kind, 2.0]
                            step (moveAt 50.0) false [] ["A", SceneEventKind.Leave, -1.0] []
                            step (moveAt 50.0) false [] [] []
            }
            test "Switching targets leaves the old processor before delivering new hit messages" {
                let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                let a = transitionTarget trace hits "A" 4.0 [0.0, 2.0] (fun _ -> true)
                let b = transitionTarget trace hits "B" 8.0 [0.0, 5.0] (fun _ -> true)
                let tree = Aardvark.UI.PickTree<string>(Sg.ofList [b; a])
                let step = transitionStep trace hits (Map.ofList ["A", 4.0; "B", 8.0]) tree
                step movement false ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0] []
                    ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0]
                step { moveAt 8.25 with evtKind = SceneEventKind.Click } false ["B", SceneEventKind.Click, 5.0; "B", SceneEventKind.Enter, 5.0]
                    ["A", SceneEventKind.Leave, -1.0] ["B", SceneEventKind.Enter, 5.0; "B", SceneEventKind.Click, 5.0]
                step (moveAt 8.25) false ["B", SceneEventKind.Move, 5.0] [] ["B", SceneEventKind.Move, 5.0]
                step movement false ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0] ["B", SceneEventKind.Leave, -1.0]
                    ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0]
                step (moveAt 50.0) false [] ["A", SceneEventKind.Leave, -1.0] []
            }
            test "Multiple shapes share one transition state and one primary callback" {
                for primaryContinue in [false; true] do
                    let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                    let tree = Aardvark.UI.PickTree<string>(transitionTarget trace hits "A" 4.0 [0.0, 2.0; 0.0, 5.0; 3.0, 5.0] (fun _ -> primaryContinue))
                    let step = transitionStep trace hits (Map.ofList ["A", 4.0]) tree
                    step movement (not primaryContinue) ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0] []
                        ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0]
                    for x, depth, kind in [4.25, 2.0, SceneEventKind.Click; 7.25, 5.0, SceneEventKind.Move; 4.25, 2.0, SceneEventKind.Move] do
                        step { moveAt x with evtKind = kind } (not primaryContinue) ["A", kind, depth] [] ["A", kind, depth]
                    step (moveAt 50.0) false [] ["A", SceneEventKind.Leave, -1.0] []
            }
            test "Overlapping targets keep primary and Enter continuation independent across repeated hits" {
                for mask in 0 .. 63 do
                    let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                    let flags offset = [for bit in offset .. offset + 2 -> mask &&& (1 <<< bit) <> 0]
                    let primary, entry = flags 0, flags 3
                    let targets = ["A", 2.0; "B", 5.0; "C", 8.0]
                    let scenes = List.map3 (fun (name, depth) p e ->
                        transitionTarget trace hits name 4.0 [0.0, depth] (function SceneEventKind.Enter -> e | _ -> p)) targets primary entry
                    let tree = Aardvark.UI.PickTree<string>(Sg.ofList (List.rev scenes))
                    let step = transitionStep trace hits (Map.ofList ["A", 4.0; "B", 4.0; "C", 4.0]) tree
                    // Both independent traversal prefixes include the first stopping processor.
                    let prefix decisions = min 3 (1 + (decisions |> List.takeWhile id |> List.length))
                    let visible = targets |> List.take (prefix primary)
                    let active = visible |> List.truncate (prefix entry)
                    let eager = [for name, depth in visible do
                                    yield name, SceneEventKind.Move, depth
                                    if List.contains (name, depth) active then yield name, SceneEventKind.Enter, depth]
                    let body = [for name, depth in visible do
                                    if List.contains (name, depth) active then yield name, SceneEventKind.Enter, depth
                                    yield name, SceneEventKind.Move, depth]
                    let consumed = not (List.forall id primary)
                    step movement consumed eager [] body
                    for kind in [SceneEventKind.Move; SceneEventKind.Click; SceneEventKind.Move] do
                        let expected = [for name, depth in visible -> name, kind, depth]
                        step { movement with evtKind = kind } consumed expected [] expected
                    step (moveAt 50.0) false [] [for name, _ in List.rev active -> name, SceneEventKind.Leave, -1.0] []
                    step (moveAt 50.0) false [] [] []
            }
            test "Multiple exits retain reverse visitation callback and message order" {
                let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                let targets = ["A", 2.0; "B", 5.0; "C", 8.0]
                let scenes = [for name, depth in targets -> transitionTarget trace hits name 4.0 [0.0, depth] (fun _ -> true)]
                let tree = Aardvark.UI.PickTree<string>(Sg.ofList (List.rev scenes))
                let step = transitionStep trace hits (Map.ofList ["A", 4.0; "B", 4.0; "C", 4.0]) tree
                let eager = [for name, depth in targets do
                                yield name, SceneEventKind.Move, depth
                                yield name, SceneEventKind.Enter, depth]
                let body = [for name, depth in targets do
                                yield name, SceneEventKind.Enter, depth
                                yield name, SceneEventKind.Move, depth]
                step movement false eager [] body
                step (moveAt 50.0) false [] ["C", SceneEventKind.Leave, -1.0; "B", SceneEventKind.Leave, -1.0; "A", SceneEventKind.Leave, -1.0] []
            }
            test "Processors whose entry is suppressed never receive an unmatched Leave" {
                let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                let near = transitionTarget trace hits "A" 4.0 [0.0, 2.0] (function SceneEventKind.Enter -> false | _ -> true)
                let far = transitionTarget trace hits "B" 4.0 [0.0, 5.0] (fun _ -> true)
                let tree = Aardvark.UI.PickTree<string>(Sg.ofList [far; near])
                let step = transitionStep trace hits (Map.ofList ["A", 4.0; "B", 4.0]) tree
                step movement false ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0; "B", SceneEventKind.Move, 5.0] []
                    ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Move, 5.0]
                step (moveAt 50.0) false [] ["A", SceneEventKind.Leave, -1.0] []
                step (moveAt 50.0) false [] [] []
            }
            test "Suppressed active entries leave once and cached Enter decisions remain authoritative" {
                let trace, hits = ResizeArray<string>(), ResizeArray<string * SceneHit>()
                let mutable primary = true
                let mutable entry = false
                let a = transitionTarget trace hits "A" 4.0 [0.0, 2.0] (function SceneEventKind.Enter -> entry | _ -> primary)
                let b = transitionTarget trace hits "B" 4.0 [0.0, 5.0] (fun _ -> true)
                let tree = Aardvark.UI.PickTree<string>(Sg.ofList [b; a])
                let step = transitionStep trace hits (Map.ofList ["A", 4.0; "B", 4.0]) tree
                step movement false ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0; "B", SceneEventKind.Move, 5.0] []
                    ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Move, 5.0]
                entry <- true
                step movement false ["A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Move, 5.0] []
                    ["A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Move, 5.0]
                step (moveAt 50.0) false [] ["A", SceneEventKind.Leave, -1.0] []
                step movement false ["A", SceneEventKind.Move, 2.0; "A", SceneEventKind.Enter, 2.0; "B", SceneEventKind.Move, 5.0; "B", SceneEventKind.Enter, 5.0] []
                    ["A", SceneEventKind.Enter, 2.0; "A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Enter, 5.0; "B", SceneEventKind.Move, 5.0]
                primary <- false
                step movement true ["A", SceneEventKind.Move, 2.0] ["B", SceneEventKind.Leave, -1.0] ["A", SceneEventKind.Move, 2.0]
                step movement true ["A", SceneEventKind.Move, 2.0] [] ["A", SceneEventKind.Move, 2.0]
                primary <- true
                step movement false ["A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Move, 5.0; "B", SceneEventKind.Enter, 5.0] []
                    ["A", SceneEventKind.Move, 2.0; "B", SceneEventKind.Enter, 5.0; "B", SceneEventKind.Move, 5.0]
            }
            test "Leave callback and message exceptions stay deferred with exact identity" {
                for throwInCallback in [false; true] do
                    let trace = ResizeArray<string>()
                    let failure = InvalidOperationException("deferred Leave failure")
                    let leave hit =
                        checkHit "near" 2.0 SceneEventKind.Leave hit
                        trace.Add "leave-call"
                        if throwInCallback then raise failure
                        true, seq { trace.Add "leave-enum"; yield "prefix"; raise failure }
                    let tree = Aardvark.UI.PickTree<string>(geometry 2.0 [
                        SceneEventKind.Move, (fun _ -> true, Seq.empty)
                        SceneEventKind.Enter, (fun _ -> true, Seq.empty)
                        SceneEventKind.Leave, leave
                    ])
                    tree.Perform movement |> snd |> Seq.iter ignore
                    let consumed, messages = tree.Perform(moveAt 50.0)
                    Expect.isFalse consumed "Leave failure does not affect primary consumption"
                    Expect.isEmpty trace "Leave callback remains deferred until enumeration"
                    let delivered = ResizeArray<string>()
                    let caught = try messages |> Seq.iter delivered.Add; None with error -> Some error
                    Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, failure))) "Propagate the same deferred exception"
                    Expect.equal (List.ofSeq delivered) (if throwInCallback then [] else ["prefix"]) "Preserve the delivered prefix"
                    Expect.equal (List.ofSeq trace) (if throwInCallback then ["leave-call"] else ["leave-call"; "leave-enum"]) "Do not invoke or enumerate Leave twice"
                    tree.Perform(moveAt 50.0) |> snd |> Seq.iter ignore
                    Expect.equal trace.Count (if throwInCallback then 1 else 2) "Repeated misses do not repeat a failed Leave"
            }
        ]
