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

    [<Tests>]
    let tests =
        testList "Pick Tree Tests" [
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
        ]
