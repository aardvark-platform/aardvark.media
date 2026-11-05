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

            testList "Querying" [
                Querying.slotExistsInProgressCallback
                Querying.slotExistsWhenStartedDuringTick false
                Querying.slotExistsWhenStartedDuringTick true
            ]
        ]