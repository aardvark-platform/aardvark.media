namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.UI.Animation
open FSharp.Data.Adaptive
open Expecto

module ``Animator Tests`` =

    module Processing =

        let animationStartedDuringTickIsUpdated =
            test "Animation started during tick is updated" {
                use _ = Animator.initTest()

                let started = ref false
                let updated = ref false

                let a1 =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun _ _ model ->
                        started.Value <- true
                        model
                    )
                    |> Animation.onProgress(fun _ _ model ->
                        updated.Value <- true
                        model
                    )

                let a2 =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun _ _ model ->
                        model |> Animator.createAndStart "a1" a1
                    )

                Animator.createAndStart "Test" a2 ()
                Animator.tickSeconds 1.0

                Expect.isTrue started.Value "Animation not started"
                Expect.isTrue updated.Value "Animation not updated"     
            }

    module private Scheduling =

        type Model = { Animator: Animator<Model>; Effects: int }
        type Factory = Model -> IAnimation<Model, float>
        type Enqueue = Factory -> int ref -> Model -> Model

        let private lens : Aether.Lens<Model, Animator<Model>> =
            (fun m -> m.Animator), (fun a m -> { m with Animator = a })
        let private initial() = { Animator = Animator.initial lens; Effects = 0 }
        let private tick time m = Animator.tick (GlobalTime.ofSeconds time) m
        let private source duration : IAnimation<Model, float> = Animation.create id |> Animation.seconds duration
        let private keys m = Animator.threads m.Animator |> fun pool -> pool.store |> HashMap.toList |> List.map fst
        let private slot m = Animator.getSlot "queue" m
        let private finished() =
            let m = initial() |> Animator.createAndStart "queue" (source 1.0) |> tick 0.0 |> tick 1.0
            Expect.isTrue (slot m).Current.IsFinished "The sole slot has completed"
            Expect.equal m.Animator.Slots.Count 1 "No unrelated slot can supply a tick"
            m

        let private factory (events: ResizeArray<string * float * int>) name duration : Factory =
            fun m ->
                events.Add($"factory-{name}", 0.0, m.Effects)
                let observe kind _ value m =
                    events.Add($"{kind}-{name}", value, m.Effects)
                    { m with Effects = m.Effects + 1 }
                source duration
                |> Animation.onStart (observe "start")
                |> Animation.onProgress (observe "progress")
                |> Animation.onFinalize (observe "finish")

        let private helpers : (string * float option * Enqueue) list = [
            "createAndPerformDelayed", Some 0.75, (fun f actions ->
                Animator.createAndPerformDelayed "queue" f (fun a ->
                    actions.Value <- actions.Value + 1
                    a.Start(LocalTime.ofSeconds 0.75)))
            "createDelayed", None, (fun f _ -> Animator.createDelayed "queue" f)
            "createAndStartDelayed", Some 0.0, (fun f _ -> Animator.createAndStartDelayed "queue" f)
            "createAndStartFromDelayed", Some 1.0, (fun f _ -> Animator.createAndStartFromDelayed "queue" f 0.25)
            "createAndStartFromLocalDelayed", Some 1.5, (fun f _ -> Animator.createAndStartFromLocalDelayed "queue" f (LocalTime.ofSeconds 1.5))
        ]

        let delayed = [
            for name, start, enqueue in helpers do
                testCase $"{name} requests a tick after the sole slot finishes" (fun () ->
                    for rate in [15; 30; 60] do
                        let events = ResizeArray<_>()
                        let actions = ref 0
                        let m = finished()
                        let m = { m with Animator = { m.Animator with TickRate = rate } }
                        let previous = (slot m).Current
                        let queued = enqueue (factory events "next" 4.0) actions m
                        Expect.equal (slot queued).Pending 1 "Exactly one entry is queued"
                        Expect.isEmpty events "The factory and callbacks remain deferred"
                        Expect.equal actions.Value 0 "The custom action remains deferred"
                        for _ in 1 .. 3 do
                            Expect.equal (keys queued) [$"animationTicks_{rate}"] "Finished pending work requests the existing tick command"
                        Expect.isEmpty events "Inspecting thread demand cannot run the factory or callbacks"
                        Expect.equal actions.Value 0 "Inspecting thread demand cannot run the queued action"
                        Expect.isTrue (obj.ReferenceEquals(previous, (slot queued).Current)) "Enqueue does not replace the current instance before a tick"
                        let m = queued |> tick 2.0
                        let current : IAnimationInstance<Model, float> = Animator.get "queue" m
                        Expect.isFalse (obj.ReferenceEquals(previous, current)) "The tick installs the queued instance"
                        Expect.equal (slot m).Pending 0 "The tick drains this entry"
                        Expect.equal actions.Value (if name = "createAndPerformDelayed" then 1 else 0) "The requested custom action runs exactly once"
                        match start with
                        | Some position ->
                            Expect.isTrue current.IsRunning "Start helpers activate the next animation"
                            Expect.equal current.Position (LocalTime.ofSeconds position) "Retain the requested start position"
                            Expect.equal current.Value (position / 4.0) "Retain the value at the requested start position"
                            Expect.equal (List.ofSeq events) [("factory-next", 0.0, 0); ("start-next", position / 4.0, 0); ("progress-next", position / 4.0, 1)] "Factory, action and callback commit order"
                            Expect.equal (keys m) [$"animationTicks_{rate}"] "Running work continues to request ticks"
                        | None ->
                            Expect.isTrue current.IsStopped "createDelayed does not implicitly start its animation"
                            Expect.equal current.Position LocalTime.zero "Stopped position remains zero"
                            Expect.equal (List.ofSeq events) [("factory-next", 0.0, 0)] "Creating a stopped instance invokes no animation callbacks"
                            Expect.isEmpty (keys m) "A stopped replacement does not keep polling")
        ]

        let fifo =
            test "Finished pending entries wake once and progress in FIFO commit order" {
                let events = ResizeArray<_>()
                let m =
                    finished()
                    |> Animator.createAndStartFromDelayed "queue" (factory events "A" 2.0) 0.25
                    |> Animator.createAndStartDelayed "queue" (factory events "B" 1.0)
                    |> Animator.createDelayed "queue" (factory events "C" 1.0)
                Expect.equal (slot m).Pending 3 "All entries remain pending until a finite tick"
                Expect.isEmpty events "Factories and callbacks are deferred for the whole queue"
                Expect.equal (keys m) ["animationTicks_30"] "The queue wakes the otherwise idle animator"
                let m = m |> tick 2.0
                Expect.equal (slot m).Pending 2 "Only the first entry is activated"
                Expect.equal (List.ofSeq events) [("factory-A", 0.0, 0); ("start-A", 0.25, 0); ("progress-A", 0.25, 1)] "First entry starts at its requested normalized position"
                let m = m |> tick 3.5
                Expect.equal (slot m).Pending 1 "Completion activates the second entry, not the third"
                Expect.equal (List.ofSeq events |> List.skip 3) [("progress-A", 1.0, 2); ("finish-A", 1.0, 3); ("factory-B", 0.0, 4); ("start-B", 0.0, 4)] "The next factory sees previous finalization effects before it starts"
                let m = m |> tick 4.0 |> tick 4.5
                Expect.equal (List.ofSeq events |> List.skip 7) [("progress-B", 0.5, 5); ("progress-B", 1.0, 6); ("finish-B", 1.0, 7); ("factory-C", 0.0, 8)] "FIFO preserves progress and commit order"
                Expect.equal m.Effects 8 "Every callback contributes once"
                Expect.equal (slot m).Pending 0 "The final entry was instantiated"
                Expect.isTrue (slot m).Current.IsStopped "The final createDelayed entry remains stopped"
                Expect.isEmpty (keys m) "The stopped tail does not poll"
            }

        let idleControls =
            test "Empty finished paused and stopped slots do not poll without eligible work" {
                Expect.isEmpty (keys (initial())) "An empty animator does not poll"
                Expect.isEmpty (keys (finished())) "A finished slot with no pending entries does not poll"
                let events = ResizeArray<_>()
                let m = initial() |> Animator.createAndStart "queue" (source 4.0) |> tick 0.0
                let m = m |> Animator.createAndStartDelayed "queue" (factory events "blocked" 1.0)
                Expect.equal (keys m) ["animationTicks_30"] "A running slot retains its normal tick command"
                let m = m |> Animator.pause "queue"
                Expect.equal (keys m) ["animationTicks_30"] "Uncommitted pause still requests a tick"
                let m = m |> tick 1.0
                Expect.isTrue (slot m).Current.IsPaused "Pause has been committed"
                Expect.equal (slot m).Pending 1 "Pending work remains behind the paused instance"
                Expect.isEmpty (keys m) "A paused slot with a queue does not poll"
                let m = m |> Animator.stop "queue" |> tick 1.5
                Expect.isTrue (slot m).Current.IsStopped "Stop has been committed"
                Expect.equal (slot m).Pending 1 "Stopping does not activate pending work"
                Expect.isEmpty (keys m) "A stopped slot with a queue does not poll"
                Expect.isEmpty events "Blocked factories and callbacks were never invoked"
            }

        let private cleanup model =
            model |> Animator.removeFinished |> Animator.removeFinished |> Animator.removeFinished

        let private retained name before after =
            Expect.isTrue (Animator.exists name after) $"{name}: cleanup keeps the slot"
            Expect.isTrue (obj.ReferenceEquals(Animator.getSlot name before, Animator.getSlot name after)) $"{name}: exact slot identity"
            Expect.isTrue (obj.ReferenceEquals((Animator.getSlot name before).Current, (Animator.getSlot name after).Current)) $"{name}: exact instance identity"

        let cleanupDelayed =
            test "Finished cleanup preserves every delayed helper until completion" {
                let failures = ResizeArray<string>()
                for name, start, enqueue in helpers do
                    try
                        let events = ResizeArray<_>()
                        let actions = ref 0
                        let queued = finished() |> enqueue (factory events "next" 4.0) actions
                        Expect.isTrue (slot queued).Current.IsFinished "Committed predecessor is finished"
                        Expect.isFalse (slot queued).Current.OutOfDate "Only the delayed entry keeps this slot alive"
                        let cleaned = cleanup queued
                        retained "queue" queued cleaned
                        Expect.equal (slot cleaned).Pending 1 "Cleanup preserves the pending entry"
                        Expect.isEmpty events "Cleanup cannot evaluate the factory or notify observers"
                        Expect.equal actions.Value 0 "Cleanup cannot perform a delayed action"
                        let activated = cleaned |> tick 2.0
                        Expect.equal (slot activated).Pending 0 "The next tick instantiates exactly one entry"
                        Expect.equal actions.Value (if name = "createAndPerformDelayed" then 1 else 0) "The delayed action runs exactly once"
                        let current : IAnimationInstance<Model, float> = Animator.get "queue" activated
                        let running, time, position =
                            match start with
                            | Some position -> activated, 2.0, position
                            | None ->
                                Expect.isTrue current.IsStopped "createDelayed does not start implicitly"
                                Expect.equal (List.ofSeq events) [("factory-next", 0.0, 0)] "No implicit start callback"
                                let pending = activated |> Animator.start "queue" |> cleanup
                                Expect.isEmpty (events |> Seq.skip 1) "Cleanup does not commit a deferred start"
                                pending |> tick 2.5, 2.5, 0.0
                        let current : IAnimationInstance<Model, float> = Animator.get "queue" running
                        Expect.isTrue current.IsRunning "Requested start is committed on the tick"
                        Expect.equal current.Position (LocalTime.ofSeconds position) "Requested local start position"
                        Expect.equal current.Value (position / 4.0) "Requested normalized start value"
                        Expect.equal (List.ofSeq events) [("factory-next", 0.0, 0); ("start-next", position / 4.0, 0); ("progress-next", position / 4.0, 1)] "Factory then requested action then callbacks"
                        let kept = cleanup running
                        retained "queue" running kept
                        let completed = kept |> tick (time + 4.0 - position)
                        Expect.isTrue (slot completed).Current.IsFinished "Replacement finishes normally"
                        Expect.equal completed.Effects 4 "Each callback contributes exactly once"
                        Expect.equal (List.ofSeq events |> List.skip 3) [("progress-next", 1.0, 2); ("finish-next", 1.0, 3)] "Final callback ordering"
                        let removed = completed |> cleanup |> tick 100.0 |> cleanup
                        Expect.isFalse (Animator.exists "queue" removed) "Idle completed replacement can finally be removed"
                        Expect.equal events.Count 5 "Repeated cleanup and ticks do not repeat callbacks"
                    with e -> failures.Add $"{name}: {e.Message}"
                Expect.isEmpty failures (String.concat "\n" failures)
            }

        let cleanupRestarts =
            test "Finished cleanup preserves every uncommitted start and restart" {
                let starts : (string * float * (Model -> Model)) list = [
                    "start", 0.0, Animator.start "queue"
                    "startOrResume", 0.0, Animator.startOrResume "queue"
                    "startFrom", 1.0, Animator.startFrom "queue" 0.25
                    "startFromLocal", 1.5, Animator.startFromLocal "queue" (LocalTime.ofSeconds 1.5)
                    "restart", 0.0, Animator.restart "queue"
                    "restartFrom", 2.0, Animator.restartFrom "queue" 0.5
                    "restartFromLocal", 0.75, Animator.restartFromLocal "queue" (LocalTime.ofSeconds 0.75)
                ]
                let failures = ResizeArray<string>()
                for name, position, request in starts do
                    try
                        let events = ResizeArray<_>()
                        let m = initial()
                        let completed = m |> Animator.createAndStart "queue" (factory events "current" 4.0 m) |> tick 0.0 |> tick 4.0
                        Expect.equal completed.Effects 4 "First lifecycle finished before restart request"
                        events.Clear()
                        let pending = request completed
                        Expect.isTrue (slot pending).Current.IsFinished "Start has not committed its state change"
                        Expect.isTrue (slot pending).Current.OutOfDate "Start action awaits a finite tick"
                        Expect.equal (slot pending).Pending 0 "No delayed entry can mask the action-only case"
                        let kept = cleanup pending
                        retained "queue" pending kept
                        Expect.isTrue (slot kept).Current.OutOfDate "Cleanup cannot drain pending actions"
                        Expect.isEmpty events "Cleanup invokes no restart callbacks"
                        let restarted = kept |> tick 5.0
                        let current : IAnimationInstance<Model, float> = Animator.get "queue" restarted
                        Expect.isTrue current.IsRunning "The pending action restarts the original instance"
                        Expect.equal current.Position (LocalTime.ofSeconds position) "Requested restart position"
                        Expect.equal current.Value (position / 4.0) "Requested restart value"
                        Expect.equal (List.ofSeq events) [("start-current", position / 4.0, 4); ("progress-current", position / 4.0, 5)] "No new factory; restart callbacks use the incoming model"
                        let completed = restarted |> cleanup |> tick (9.0 - position)
                        Expect.equal (List.ofSeq events |> List.skip 2) [("progress-current", 1.0, 6); ("finish-current", 1.0, 7)] "Restart completion ordering"
                        let removed = completed |> cleanup |> tick 100.0 |> cleanup
                        Expect.isFalse (Animator.exists "queue" removed) "Completed action-only work becomes removable"
                        Expect.equal removed.Effects 8 "Both complete lifecycles contribute once"
                        Expect.equal events.Count 4 "Repeated cleanup cannot duplicate restart callbacks"
                    with e -> failures.Add $"{name}: {e.Message}"
                Expect.isEmpty failures (String.concat "\n" failures)
            }

        let cleanupFifo =
            test "Repeated finished cleanup preserves queued FIFO callback effects" {
                let events = ResizeArray<_>()
                let queued =
                    finished()
                    |> Animator.createAndStartFromDelayed "queue" (factory events "A" 2.0) 0.25
                    |> Animator.createAndStartDelayed "queue" (factory events "B" 1.0)
                    |> Animator.createDelayed "queue" (factory events "C" 1.0)
                let m = cleanup queued
                retained "queue" queued m
                Expect.equal (slot m).Pending 3 "All entries survive repeated cleanup"
                Expect.isEmpty events "Cleanup is not queue processing"
                let m = m |> tick 2.0 |> cleanup
                Expect.equal (slot m).Pending 2 "Only A activates"
                Expect.equal (List.ofSeq events) [("factory-A", 0.0, 0); ("start-A", 0.25, 0); ("progress-A", 0.25, 1)] "A starts from its requested position"
                let m = m |> tick 3.5 |> cleanup
                Expect.equal (slot m).Pending 1 "A's completion activates B only"
                Expect.equal (List.ofSeq events |> List.skip 3) [("progress-A", 1.0, 2); ("finish-A", 1.0, 3); ("factory-B", 0.0, 4); ("start-B", 0.0, 4)] "B sees A's finalization effects"
                let m = m |> tick 4.0 |> cleanup |> tick 4.5 |> cleanup
                Expect.equal (List.ofSeq events |> List.skip 7) [("progress-B", 0.5, 5); ("progress-B", 1.0, 6); ("finish-B", 1.0, 7); ("factory-C", 0.0, 8)] "C sees B's finalization effects"
                Expect.equal (slot m).Pending 0 "Queue drains in FIFO order"
                Expect.isTrue (slot m).Current.IsStopped "The stopped tail is retained, not silently started"
                let m = m |> Animator.start "queue" |> cleanup |> tick 5.0 |> cleanup |> tick 6.0
                Expect.equal (List.ofSeq events |> List.skip 11) [("start-C", 0.0, 8); ("progress-C", 0.0, 9); ("progress-C", 1.0, 10); ("finish-C", 1.0, 11)] "Tail explicitly starts and completes once"
                let removed = cleanup m
                Expect.equal removed.Effects 12 "FIFO preserves every model effect"
                Expect.isFalse (Animator.exists "queue" removed) "Completed queue is eventually removed"
            }

        let cleanupStates =
            test "Finished cleanup classifies empty idle active and mixed slots without side effects" {
                let empty = initial() |> cleanup
                Expect.isTrue empty.Animator.Slots.IsEmpty "Empty cleanup is harmless"
                let idle = finished() |> cleanup
                Expect.isTrue idle.Animator.Slots.IsEmpty "Idle finished slots are removed"
                let events = ResizeArray<_>()
                let m =
                    finished()
                    |> Animator.createAndStart "running" (source 4.0)
                    |> Animator.createAndStart "paused" (source 4.0)
                    |> Animator.create "stopped" (source 4.0)
                    |> Animator.createAndStart "done" (source 0.0)
                    |> Animator.createAndStart "restart" (source 0.0)
                    |> tick 2.0
                    |> Animator.pause "paused"
                    |> tick 2.5
                    |> Animator.createAndStartDelayed "queue" (factory events "later" 4.0)
                    |> Animator.restart "restart"
                Expect.isTrue (Animator.getUntyped "running" m).IsRunning "Running control"
                Expect.isTrue (Animator.getUntyped "paused" m).IsPaused "Paused control"
                Expect.isTrue (Animator.getUntyped "stopped" m).IsStopped "Stopped control"
                let cleaned = cleanup m
                Expect.isFalse (Animator.exists "done" cleaned) "Mixed cleanup removes only idle finished slots"
                for name in ["running"; "paused"; "stopped"; "queue"; "restart"] do retained name m cleaned
                Expect.equal cleaned.Animator.Slots.Count 5 "Active and protected finished slots survive together"
                Expect.equal (cleaned.Effects, cleaned.Animator.TickRate, cleaned.Animator.CurrentTick)
                    (m.Effects, m.Animator.TickRate, m.Animator.CurrentTick) "Cleanup only filters slots"
                Expect.isEmpty events "Mixed cleanup cannot execute queued factories"
            }

        let cleanupExplicitRemoval =
            test "Explicit remove and removeAll still discard queued work and restarts" {
                for pending, restart in [true, false; false, true; true, true] do
                    for all, remove in [false, Animator.remove "queue"; true, Animator.removeAll] do
                        let events = ResizeArray<_>()
                        let actions = ref 0
                        let m = finished() |> Animator.create "other" (source 1.0)
                        let m = if pending then Animator.createAndPerformDelayed "queue" (factory events "discarded" 1.0) (fun a -> actions.Value <- actions.Value + 1; a.Start()) m else m
                        let m = if restart then Animator.restart "queue" m else m
                        let discarded = slot m
                        let removed = m |> remove |> tick 100.0 |> cleanup
                        Expect.isFalse (Animator.exists "queue" removed) "Explicit removal overrides work preservation"
                        Expect.equal removed.Animator.Slots.Count (if all then 0 else 1) "removeAll clears every slot; remove keeps unrelated slots"
                        if not all then retained "other" m removed
                        Expect.equal discarded.Pending (if pending then 1 else 0) "Removal does not process the discarded queue"
                        if restart then
                            Expect.isTrue discarded.Current.IsFinished "Removal does not commit a pending restart"
                            Expect.isTrue discarded.Current.OutOfDate "Discarded actions were never executed"
                        Expect.isEmpty events "Discarded factories and callbacks never execute"
                        Expect.equal actions.Value 0 "Discarded delayed actions never execute"
                        Expect.equal removed.Effects m.Effects "Discarding work has no callback effects"
            }

    module Querying =

        let slotExistsInProgressCallback =
            test "Slot exists in onProgress callback" {
                use _ = Animator.initTest()

                let animation =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.onProgress (fun name _ model ->
                        Expect.isTrue (model |> Animator.exists name) "Exists returns false"
                        model |> Animator.getUntyped name |> ignore
                        model
                    )

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 1.0
                Animator.tickSeconds 3.0
                Animator.tickSeconds 1336.0
            }

        let slotExistsWhenStartedDuringTick (delay: bool) =
            let name =
                let baseName = "Slot exists when started during tick"
                if delay then $"{baseName} (delay)" else baseName

            test name {
                use _ = Animator.initTest()

                let a1 =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun name _ model ->
                        Expect.isTrue (model |> Animator.exists name) "Exists returns false"
                        model |> Animator.getUntyped name |> ignore
                        model
                    )

                let a2 =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun _ _ model ->
                        if delay then
                            model |> Animator.createAndStartDelayed "a1" (fun _ -> a1)
                        else
                            model |> Animator.createAndStart "a1" a1
                    )

                Animator.createAndStart "a2" a2 ()
                Animator.tickSeconds 1.0
                Animator.tickSeconds 3.0
                Animator.tickSeconds 1336.0
            }

    [<Tests>]
    let tests =
        testList "Primitives.Animation.Animator" [
            testList "Processing" [
                Processing.animationStartedDuringTickIsUpdated
            ]

            testList "Scheduling" [
                yield! Scheduling.delayed
                Scheduling.fifo
                Scheduling.idleControls
            ]

            testList "Cleanup" [
                Scheduling.cleanupDelayed
                Scheduling.cleanupRestarts
                Scheduling.cleanupFifo
                Scheduling.cleanupStates
                Scheduling.cleanupExplicitRemoval
            ]

            testList "Querying" [
                Querying.slotExistsInProgressCallback
                Querying.slotExistsWhenStartedDuringTick false
                Querying.slotExistsWhenStartedDuringTick true
            ]
        ]