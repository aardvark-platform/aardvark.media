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