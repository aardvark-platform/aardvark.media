namespace Aardvark.UI.Tests

open Aardvark.UI.Animation
open Expecto

module ``Simple Tests`` =

    module Events =
        let startFinish =
            test "Start / Finish" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 4.0
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 3.0

                Expect.checkEvents events [
                    EventType.Progress, 0.5
                ]

                Animator.tickSeconds 1336.0

                Expect.checkEvents events [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let pauseResume =
            test "Pause / Resume" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                // Resume is NOP if not paused
                Animator.resume "Test" ()
                Animator.tickSeconds 0.4

                Expect.checkEvents events [
                    EventType.Progress, 0.4
                ]

                // Pause, resume should cancel out
                Animator.pause "Test" ()
                Animator.resume "Test" ()
                Animator.tickSeconds 0.5

                Expect.checkEvents events [
                    EventType.Pause, 0.4
                    EventType.Resume, 0.4
                    EventType.Progress, 0.5
                ]

                Animator.pause "Test" ()
                Animator.tickSeconds 0.7

                Expect.checkEvents events [
                    EventType.Pause, 0.5 // Pause does not evaluate
                ]

                // Pause is NOP if already paused
                Animator.pause "Test" ()
                Animator.tickSeconds 0.8
                Animator.tickSeconds 0.9

                Expect.checkEvents events []

                Animator.resume "Test" ()
                Animator.tickSeconds 1.0
                Animator.tickSeconds 1.1

                Expect.checkEvents events [
                    EventType.Resume, 0.5
                    EventType.Progress, 0.7 // Paused in between tick 0.5 and 0.7 -> progress continues at +0.2
                    EventType.Progress, 0.8
                ]

                Animator.tickSeconds 1.3

                Expect.checkEvents events [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let pauseFinish =
            test "Pause / Finish" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 0.99

                Expect.checkEvents events [
                    EventType.Progress, 0.99
                ]

                Animator.pause "Test" ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Pause, 0.99
                ]

                Animator.resume "Test" ()
                Animator.tickSeconds 1.0001

                Expect.checkEvents events [
                    EventType.Resume, 0.99
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let stopRestartFrom =
            test "Stop / Restart from" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                // Start is NOP if already running
                Animator.start "Test" ()
                Animator.tickSeconds 0.5

                Expect.checkEvents events [
                    EventType.Progress, 0.5
                ]

                Animator.stop "Test" ()
                Animator.tickSeconds 0.6

                Expect.checkEvents events [
                    EventType.Stop, 0.0 // Stop resets to the start
                ]

                // Stop is NOP if already paused
                Animator.stop "Test" ()
                Animator.tickSeconds 0.7

                Expect.checkEvents events []

                Animator.start "Test" ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.restartFrom "Test" 0.8 ()
                Animator.tickSeconds 1.5

                Expect.checkEvents events [
                    EventType.Start, 0.8
                    EventType.Progress, 0.8
                ]

                // Restarted from 0.8 at tick 1.5, so it will finish at 1.7
                Animator.tickSeconds 1.7

                Expect.checkEvents events [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let stopFinish =
            test "Stop / Finish" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 0.99

                Expect.checkEvents events [
                    EventType.Progress, 0.99
                ]

                Animator.stop "Test" ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Stop, 0.0
                ]

                Animator.start "Test" ()
                Animator.tickSeconds 1.0001

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]
            }

        let startFrom =
            test "Start from" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                Animator.createAndStartFrom "Test" animation 0.75 ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFrom "Test" -0.25 ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.restartFrom "Test" 1.25 ()
                Animator.tickSeconds 3.0

                Expect.checkEvents events [
                    EventType.Start, 1.0
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let startFromLooped =
            test "Start from (looped)" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 3
                    |> Animation.trackEvents

                Animator.createAndStartFrom "Test" animation 0.75 ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFrom "Test" 1.75 ()
                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Start, 0.25
                    EventType.Progress, 0.25
                ]

                Animator.restartFrom "Test" 2.75 ()
                Animator.tickSeconds 2.0

                Expect.checkEvents events [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFrom "Test" 3.01 ()
                Animator.tickSeconds 3.0

                Expect.checkEvents events [
                    EventType.Start, 1.0
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let zeroDuration =
            test "Zero duration" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.seconds 0.0
                    |> Animation.ease (Easing.In EasingFunction.Quadratic)
                    |> Animation.loopN LoopMode.Mirror 3
                    |> Animation.trackEvents

                Expect.equal animation.Duration Duration.zero "Unexpected duration"
                Expect.equal animation.TotalDuration Duration.zero "Unexpected total duration"

                Animator.createAndStartFrom "Test" animation 0.0 ()
                Animator.tickSeconds 0.0
                Animator.tickSeconds 52.0

                Expect.checkEvents events [
                    EventType.Start, 1.0
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]

                let inst = Animator.get "Test" ()
                Expect.equal inst.Value 1.0 "Unexpected value"
            }

        let infiniteDuration =
            test "Infinite duration" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create id
                    |> Animation.duration Duration.infinite
                    |> Animation.ease (Easing.In EasingFunction.Quadratic)
                    |> Animation.loopN LoopMode.Mirror 3
                    |> Animation.trackEvents

                Expect.equal animation.Duration Duration.infinite "Unexpected duration"
                Expect.equal animation.TotalDuration Duration.infinite "Unexpected total duration"

                Animator.createAndStartFrom "Test" animation 0.0 ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 134.0

                Expect.checkEvents events [
                    EventType.Progress, 0.0
                ]
            }

    module private Mapping =
        open System
        open System.Runtime.CompilerServices
        open Aardvark.Base

        let private create (animation : IAnimation<'Model, 'Value>) = animation.Create(Sym.ofString "Mapping")
        let private child<'Model> : IAnimation<'Model, float> = Animation.create id |> Animation.seconds 1.0
        let private sameError expected action =
            let actual = try action(); None with error -> Some error
            Expect.isTrue (actual |> Option.exists (fun error -> obj.ReferenceEquals(error, expected))) "Original exception identity"

        let deferred dependent =
            let mutable calls = 0
            let evaluate model value = calls <- calls + 1; float calls + float model + value
            let animation =
                if dependent then child<int> |> Animation.map' evaluate
                else child<int> |> Animation.map (evaluate 0)
            let instance = create animation
            for _ in 1 .. 2 do Expect.equal instance.Value 0.0 "Default before first successful commit"
            Expect.equal calls 0 "Initial reads do not invoke the mapper"
            instance.Perform(Action.Start LocalTime.zero)
            Expect.equal (instance.Commit(10, GlobalTime.zero)) 10 "Returned model"
            Expect.equal calls 0 "Commit does not evaluate Value"
            let model = if dependent then 10.0 else 0.0
            Expect.equal instance.Value (model + 1.0) "First deferred read"
            Expect.equal instance.Value (model + 2.0) "Repeated reads evaluate again"
            instance.Perform(Action.Update(LocalTime.ofSeconds 0.5, false))
            Expect.equal instance.Value (model + 3.0) "Queued input changes are not committed yet"
            Expect.equal (instance.Commit(20, GlobalTime.zero)) 20 "Next commit returns its model"
            Expect.equal calls 3 "Successive commits remain lazy"
            Expect.equal instance.Value ((if dependent then 20.0 else 0.0) + 4.5) "Read uses latest model and current input"
            instance.Commit(30, GlobalTime.zero) |> ignore
            Expect.equal calls 4 "Idle commit stays lazy"
            Expect.equal instance.Value ((if dependent then 30.0 else 0.0) + 5.5) "Idle commit refreshes retained model"
            let reference = child<int> |> Animation.map (fun _ -> "mapped") |> create
            Expect.isNull reference.Value "Reference-valued output initially defaults to null"
            reference.Commit(0, GlobalTime.zero) |> ignore
            Expect.equal reference.Value "mapped" "Even a stopped input initializes on a successful commit"

        let models () =
            let input =
                child<int>
                |> Animation.onStart (fun _ _ model -> model + 10)
                |> Animation.onProgress (fun _ _ model -> model * 10)
            let instance = input |> Animation.map' (fun model value -> float model + 100.0 * value) |> create
            instance.Perform(Action.Start LocalTime.zero)
            Expect.equal (instance.Commit(1, GlobalTime.zero)) 11 "Child Start returns changed model"
            Expect.equal instance.Value 11.0 "Value uses child-returned model"
            instance.Perform(Action.Update(LocalTime.ofSeconds 0.5, false))
            Expect.equal (instance.Commit(2, GlobalTime.zero)) 20 "Child Progress returns changed model"
            Expect.equal instance.Value 70.0 "Value sees Progress model and current input"
            Expect.equal (instance.Commit(3, GlobalTime.zero)) 3 "Idle commit returns its supplied model"
            Expect.equal instance.Value 53.0 "Successive idle model replaces the old one"

            let mutable seen = Unchecked.defaultof<StrongBox<int>>
            let input = child<StrongBox<int>> |> Animation.onStart (fun _ _ model -> StrongBox<int>(model.Value + 10))
            let reference =
                input |> Animation.map' (fun model value ->
                    seen <- model
                    float (if isNull model then 99 else model.Value) + value) |> create
            reference.Perform(Action.Start LocalTime.zero)
            let original = StrongBox<int>(1)
            let returned = reference.Commit(original, GlobalTime.zero)
            Expect.isFalse (obj.ReferenceEquals(original, returned)) "Child returned a fresh reference model"
            Expect.equal reference.Value 11.0 "Reference-model result"
            Expect.isTrue (obj.ReferenceEquals(seen, returned)) "Retain the returned reference, not the input model"
            returned.Value <- 12
            Expect.equal reference.Value 12.0 "Reference-model mutations remain visible at each read"
            Expect.isNull (reference.Commit(null, GlobalTime.zero)) "A null model can commit successfully"
            Expect.equal reference.Value 99.0 "Initialization is distinct from a null retained model"

        let nested () =
            let trace = ResizeArray<string>()
            let instance =
                child<int>
                |> Animation.map (fun value -> trace.Add "inner"; value + 1.0)
                |> Animation.map' (fun model value -> trace.Add $"outer:{model}"; float model + value)
                |> create
            Expect.equal instance.Value 0.0 "Nested initial default"
            instance.Perform(Action.Start LocalTime.zero)
            instance.Commit(5, GlobalTime.zero) |> ignore
            Expect.isEmpty trace "Nested Commit does not evaluate either mapper"
            for _ in 1 .. 2 do Expect.equal instance.Value 6.0 "Nested read"
            Expect.checkEvents trace ["inner"; "outer:5"; "inner"; "outer:5"]
            instance.Perform(Action.Update(LocalTime.ofSeconds 0.5, false))
            instance.Commit(9, GlobalTime.zero) |> ignore
            Expect.isEmpty trace "Nested successive commit stays deferred"
            Expect.equal instance.Value 10.5 "Both layers use the latest commit"
            Expect.checkEvents trace ["inner"; "outer:9"]

        let commitFailure () =
            let failure = InvalidOperationException "child commit"
            let mutable fail = true
            let callback _ _ model = if fail then raise failure else model
            let mutable calls = 0
            let instance =
                child<int> |> Animation.onStart callback |> Animation.onProgress callback
                |> Animation.map' (fun model value -> calls <- calls + 1; float model + value) |> create
            instance.Perform(Action.Start LocalTime.zero)
            sameError failure (fun () -> instance.Commit(1, GlobalTime.zero) |> ignore)
            Expect.equal instance.Value 0.0 "Failed initial commit leaves Value uninitialized"
            Expect.equal calls 0 "Failed initial commit cannot enable mapping"
            fail <- false
            Expect.equal (instance.Commit(10, GlobalTime.zero)) 10 "Recovery succeeds"
            Expect.equal instance.Value 10.0 "First successfully committed model"
            instance.Perform(Action.Update(LocalTime.ofSeconds 0.5, false))
            fail <- true
            sameError failure (fun () -> instance.Commit(20, GlobalTime.zero) |> ignore)
            Expect.equal instance.Value 10.5 "Retain successful model, but read the input's latest value"
            fail <- false
            instance.Commit(30, GlobalTime.zero) |> ignore
            Expect.equal instance.Value 30.5 "Later success replaces the retained model"

        let valueFailure innerFails =
            let failure = InvalidOperationException "mapped Value"
            let mutable fail, calls = true, 0
            let mapping model value =
                calls <- calls + 1
                if fail then raise failure
                float model + value
            let failing = child<int> |> Animation.map' mapping
            let mutable outerCalls = 0
            let animation =
                if innerFails then failing |> Animation.map (fun value -> outerCalls <- outerCalls + 1; value + 1.0)
                else failing
            let instance = create animation
            Expect.equal instance.Value 0.0 "Initial default cannot throw"
            instance.Commit(10, GlobalTime.zero) |> ignore
            Expect.equal calls 0 "Throwing mapping remains deferred"
            for _ in 1 .. 2 do sameError failure (fun () -> instance.Value |> ignore)
            Expect.equal calls 2 "A failed read is retried on every read"
            Expect.equal outerCalls 0 "A failing input Value prevents outer mapping"
            instance.Commit(20, GlobalTime.zero) |> ignore
            Expect.equal calls 2 "Commit succeeds without retrying Value"
            fail <- false
            Expect.equal instance.Value (if innerFails then 21.0 else 20.0) "Read recovers with latest retained model"

        let delegation () =
            let trace = ResizeArray<Symbol * float * int>()
            let mutable calls = 0
            let definition =
                child<int> |> Animation.seconds 2.0 |> Animation.loopN LoopMode.Mirror 2
                |> Animation.easeCustom false (fun value -> value * value)
                |> Animation.map' (fun model value -> calls <- calls + 1; float model + 10.0 * value)
                |> Animation.onStart (fun name value model -> trace.Add(name, value, model); model + 1)
            let instance = create definition
            Expect.equal instance.Name (Sym.ofString "Mapping") "Name"
            Expect.isTrue (obj.ReferenceEquals(instance.Definition, definition)) "Definition identity"
            Expect.equal instance.Duration (Duration.ofSeconds 2.0) "Duration"
            Expect.equal instance.TotalDuration (Duration.ofSeconds 4.0) "Total duration"
            Expect.equal (instance.DistanceTime 0.25) (definition.DistanceTime 0.25) "Distance-time delegation"
            Expect.isTrue instance.IsStopped "Initial state"
            Expect.isFalse instance.OutOfDate "Initial action queue"
            instance.Perform(Action.Start(LocalTime.ofSeconds 0.5))
            Expect.isTrue instance.OutOfDate "Perform delegates to input"
            Expect.equal (instance.Commit(5, GlobalTime.zero)) 6 "Mapped subscription returns changed model"
            Expect.checkEvents trace [Sym.ofString "Mapping", 5.625, 5]
            Expect.equal calls 1 "Subscription evaluates only for its event"
            Expect.equal instance.Value 6.625 "Deferred read uses post-callback model"
            Expect.equal instance.Position (LocalTime.ofSeconds 0.5) "Position"
            Expect.isTrue instance.IsRunning "Running state"
            Expect.isFalse instance.OutOfDate "Commit drains delegated actions"
            instance.Perform Action.Pause
            instance.Commit(6, GlobalTime.zero) |> ignore
            Expect.isTrue instance.IsPaused "Pause delegation"
            instance.Perform Action.Resume
            instance.Commit(6, GlobalTime.zero) |> ignore
            Expect.isTrue instance.IsRunning "Resume delegation"
            instance.Perform Action.Stop
            instance.Commit(50, GlobalTime.zero) |> ignore
            Expect.isTrue instance.IsStopped "Stop delegation"
            Expect.equal instance.Position LocalTime.zero "Stop position"
            Expect.equal instance.Value 50.0 "Stop does not clear initialization"
            calls <- 0
            let unsubscribed = definition |> Animation.unsubscribeAll |> create
            unsubscribed.Perform(Action.Start(LocalTime.ofSeconds 0.5))
            Expect.equal (unsubscribed.Commit(5, GlobalTime.zero)) 5 "Unsubscription removes callback effects"
            Expect.isEmpty trace "Unsubscribed instance has no event callback"
            Expect.equal calls 0 "Unsubscribed commits remain lazy"
            Expect.equal unsubscribed.Value 5.625 "Unsubscription preserves mapping"

    module Timing =

        let private identity : IAnimation<unit, float> =
            Animation.create id |> Animation.microseconds 1L

        // Literal triangular phases avoid relying on integer parity or the production fold.
        let private mirrorCycles =
            [
                0.0, [0.0; 0.25; 0.5; 0.75; 1.0]
                1.0, [1.0; 0.75; 0.5; 0.25; 0.0]
                2147483646.0, [0.0; 0.25; 0.5; 0.75; 1.0]
                2147483647.0, [1.0; 0.75; 0.5; 0.25; 0.0]
                2147483648.0, [0.0; 0.25; 0.5; 0.75; 1.0]
                2147483649.0, [1.0; 0.75; 0.5; 0.25; 0.0]
                4294967295.0, [1.0; 0.75; 0.5; 0.25; 0.0]
                4294967296.0, [0.0; 0.25; 0.5; 0.75; 1.0]
                4294967297.0, [1.0; 0.75; 0.5; 0.25; 0.0]
            ]

        let mirrorPhases =
            test "Mirror preserves phases at large positions and their negatives" {
                let mirrored = identity |> Animation.loop LoopMode.Mirror
                for cycle, phases in mirrorCycles do
                    for quarter, expected in List.indexed phases do
                        for sign in [1.0; -1.0] do
                            let position = sign * (cycle + float quarter / 4.0)
                            Expect.equal (mirrored.DistanceTime position) expected $"Mirror phase at {position}"
            }

        let otherModes =
            test "Repeat, Continue and easing retain their behavior" {
                let repeated = identity |> Animation.loop LoopMode.Repeat
                for position, expected in [0.0, 0.0; 0.25, 0.25; 1.0, 1.0; 1.25, 0.25; 2.0, 1.0;
                                           -0.25, 0.75; -1.0, 1.0; 2147483649.25, 0.25; 4294967297.25, 0.25] do
                    Expect.equal (repeated.DistanceTime position) expected $"Repeat at {position}"
                let continued = identity |> Animation.loop LoopMode.Continue
                for position in [-4294967297.25; -1.25; 0.0; 1.0; 1.25; 2147483649.25; 4294967297.25] do
                    Expect.equal (continued.DistanceTime position) position $"Continue at {position}"
                for mode, expected in [LoopMode.Mirror, 0.5625; LoopMode.Repeat, 0.0625; LoopMode.Continue, 1.5625] do
                    let eased = identity |> Animation.loop mode |> Animation.easeCustom false (fun t -> t * t)
                    Expect.equal (eased.DistanceTime 1.25) expected "Loop wrapping precedes easing"
            }

        let finiteEndpoints =
            test "Finite mirror loops retain their final endpoint" {
                for iterations, expected in [2, 0.0; 3, 1.0] do
                    use _ = Animator.initTest()
                    let events, animation =
                        identity |> Animation.loopN LoopMode.Mirror iterations |> Animation.trackEvents
                    Expect.equal animation.TotalDuration (Duration.ofMicroseconds iterations) "Finite total duration"
                    Animator.createAndStart "Test" animation ()
                    Animator.tick GlobalTime.zero ()
                    Expect.checkEvents events [EventType.Start, 0.0; EventType.Progress, 0.0]
                    Animator.tick (GlobalTime.ofNanoseconds (int64 iterations * 1000L)) ()
                    Expect.checkEvents events [EventType.Progress, expected; EventType.Finalize, expected]
                    let instance : IAnimationInstance<unit, float> = Animator.get "Test" ()
                    Expect.equal instance.Value expected "Finite endpoint value"
                    Expect.isTrue instance.IsFinished "Finite loop is finished"
            }

        let longMirrored =
            test "Infinite mirror retains direction and Progress at large Animator ticks" {
                use _ = Animator.initTest()
                let events, animation = identity |> Animation.loop LoopMode.Mirror |> Animation.trackEvents
                Expect.equal animation.TotalDuration Duration.infinite "Infinite total duration"
                Animator.createAndStart "Test" animation ()
                Animator.tick GlobalTime.zero ()
                Expect.checkEvents events [EventType.Start, 0.0; EventType.Progress, 0.0]
                for cycle in [2147483649L; 4294967297L] do
                    Animator.tick (GlobalTime.ofNanoseconds (cycle * 1000L + 250L)) ()
                    let instance : IAnimationInstance<unit, float> = Animator.get "Test" ()
                    Expect.equal instance.Value 0.75 $"Mirrored value at cycle {cycle} + 0.25"
                    Expect.checkEvents events [EventType.Progress, 0.75]
                    Expect.isTrue instance.IsRunning "Infinite mirror remains running without Finalize"
            }

    [<Tests>]
    let tests =
        testList "Animation.Simple" [
            testList "Mapping" [
                test "Map and map' retain defaults, deferred evaluation and repeated reads" {
                    for dependent in [false; true] do Mapping.deferred dependent
                }
                testCase "Child callback models and successful idle commits are retained" Mapping.models
                testCase "Nested mappings evaluate per read in child-first order" Mapping.nested
                testCase "Commit exceptions preserve the last successful model" Mapping.commitFailure
                test "Mapping and input Value exceptions retain identity and laziness" {
                    for inner in [false; true] do Mapping.valueFailure inner
                }
                testCase "Subscriptions and delegated properties retain their behavior" Mapping.delegation
            ]
            testList "Timing" [
                Timing.mirrorPhases
                Timing.otherModes
                Timing.finiteEndpoints
                Timing.longMirrored
            ]
            testList "Events" [
                Events.startFinish
                Events.pauseResume
                Events.pauseFinish
                Events.stopRestartFrom
                Events.stopFinish
                Events.startFrom
                Events.startFromLooped
                Events.zeroDuration
                Events.infiniteDuration
            ]
        ]