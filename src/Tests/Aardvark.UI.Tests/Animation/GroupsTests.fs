namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.UI.Animation
open Expecto

module ``Groups Tests`` =

    module Sequential =

        let progress =
            test "Progress" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 2.0
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.sequential [a; b]
                    |> Animation.seconds 8.0
                    |> Animation.trackEvents

                Expect.equal c.Duration.TotalSeconds 8.0 "Unexpected duration"

                // 0.0
                Animator.createAndStart "Test" c ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                // 0.5
                Animator.tickSeconds 0.5

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.25
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, 0.25
                ]

                // 2.5
                Animator.tickSeconds 2.5

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, 0.75
                ]

                // 4.5
                Animator.tickSeconds 4.5

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                    EventType.Progress, 0.125
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, 0.125
                ]

                // 8.0
                Animator.tickSeconds 8.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]
            }

        let startFrom =
            test "Start from" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Repeat 3
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.sequential [a; b]
                    |> Animation.seconds 10.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                Expect.equal c.TotalDuration.TotalSeconds 20.0 "Unexpected duration"

                Animator.createAndStartFromLocal "Test" c (LocalTime.ofSeconds 1.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFromLocal "Test" (LocalTime.ofSeconds 3.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.25
                    EventType.Progress, 0.25
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.25
                    EventType.Progress, 0.25
                ]

                Animator.restartFromLocal "Test" (LocalTime.ofSeconds 5.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Stop, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFromLocal "Test" (LocalTime.ofSeconds 6.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.25
                    EventType.Progress, 0.25
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.25
                    EventType.Progress, 0.25
                ]

                Animator.restartFromLocal "Test" (LocalTime.ofSeconds 10.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.75
                    EventType.Progress, 0.75
                ]

                Animator.restartFromLocal "Test" (LocalTime.ofSeconds 20.5) ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Stop, 0.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]
            }

        let positionWithEasing =
            test "Position (with easing)" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 0.25
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create (~-)
                    |> Animation.seconds 0.25
                    |> Animation.loopN LoopMode.Repeat 2
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.sequential [a; b]
                    |> Animation.easeCustom false ((+) 0.1)
                    |> Animation.trackEvents

                let instance =
                    Animator.createAndStart "Test" c ()
                    Animator.getUntyped "Test" ()

                // 0.0
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.4
                    EventType.Progress, 0.4
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.4
                    EventType.Progress, 0.4
                ]

                Expect.equal instance.Position LocalTime.zero "Unexpected position"

                // 0.4
                Animator.tickSeconds 0.4

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, -0.0
                    EventType.Progress, -0.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, -0.0
                ]

                Expect.equal instance.Position (LocalTime.ofSeconds 0.4) "Unexpected position"
            }

        let pauseResumeWithEasing =
            test "Pause / Resume (with easing)" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 0.25
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create (~-)
                    |> Animation.seconds 0.25
                    |> Animation.loopN LoopMode.Repeat 2
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.sequential [a; b]
                    |> Animation.easeCustom false ((+) 0.1)
                    |> Animation.trackEvents

                Animator.createAndStart "Test" c ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.4
                    EventType.Progress, 0.4
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, 0.4
                    EventType.Progress, 0.4
                ]

                Animator.pause "Test" ()
                Animator.tickSeconds 0.4

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Pause, 0.4
                ]

                Animator.resume "Test" ()
                Animator.tickSeconds 10.0

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, -0.0
                    EventType.Progress, -0.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Resume, 0.4
                    EventType.Progress, -0.0
                ]
            }

        let outOfBoundsEasing =
            test "Out-of-bounds easing" {
                use _ = Animator.initTest()

                let eventsI, i =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.trackEvents

                let eventsO, o =
                    Animation.sequential [i]
                    |> Animation.easeCustom false ((*) 10.0)
                    |> Animation.trackEvents

                Animator.createAndStart "Test" o ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsI [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsO [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 0.5

                Expect.checkEvents eventsI [
                    EventType.Progress, 5.0
                ]

                Expect.checkEvents eventsO [
                    EventType.Progress, 5.0
                ]

                Animator.tickSeconds 1.0

                Expect.checkEvents eventsI [
                    EventType.Progress, 10.0
                    EventType.Finalize, 10.0
                ]

                Expect.checkEvents eventsO [
                    EventType.Progress, 10.0
                    EventType.Finalize, 10.0
                ]
            }

        let negativeEasing (mode: LoopMode) =
            test $"Negative easing ({mode})" {
                use _ = Animator.initTest()

                let eventsI, i =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN mode 1
                    |> Animation.trackEvents

                let eventsO, o =
                    Animation.sequential [i]
                    |> Animation.easeCustom false ((*) -1.0)
                    |> Animation.trackEvents

                Animator.createAndStart "Test" o ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsI [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsO [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 0.25

                let value =
                    match mode with
                    | LoopMode.Mirror -> 0.25
                    | LoopMode.Repeat -> 0.75
                    | _ -> -0.25

                Expect.checkEvents eventsI [
                    EventType.Progress, value
                ]

                Expect.checkEvents eventsO [
                    EventType.Progress, value
                ]

                Animator.tickSeconds 1.0

                let value =
                    match mode with
                    | LoopMode.Mirror -> 1.0
                    | LoopMode.Repeat -> 1.0
                    | _ -> -1.0

                Expect.checkEvents eventsI [
                    EventType.Progress, value
                    EventType.Finalize, value
                ]

                Expect.checkEvents eventsO [
                    EventType.Progress, value
                    EventType.Finalize, value
                ]
            }

        let empty =
            test "Empty" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.sequential []
                    |> Animation.seconds 1
                    |> Animation.trackEvents

                Animator.createAndStart "Test" animation ()
                Animator.tickSeconds 0.0

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 1.0

                Expect.checkEvents events [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]
            }

        let emptyMember =
            test "Empty member" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.empty
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 1
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.empty
                    |> Animation.trackEvents

                let eventsD, d =
                    Animation.create ((+) 0.1)
                    |> Animation.seconds 1
                    |> Animation.trackEvents

                let events, seq =
                    Animation.sequential [a; b; c; d]
                    |> Animation.seconds 4.0
                    |> Animation.trackEvents

                Expect.equal seq.Duration.TotalSeconds 4.0 "Unexpected duration"

                Animator.createAndStart "Test" seq ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsC [
                ]

                Expect.checkEvents eventsD [
                ]

                Expect.checkEvents events [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Animator.tickSeconds 2.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]

                Expect.checkEvents eventsC [
                ]

                Expect.checkEvents eventsD [
                    EventType.Start,    0.1
                    EventType.Progress, 0.1
                ]

                Expect.checkEvents events [
                    EventType.Progress, 0.1
                ]
            }

    module Concurrent =

        let progress =
            test "Progress" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 2.0
                    |> Animation.loopN LoopMode.Repeat 2
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.map2 (fun x y -> x, y) a b
                    |> Animation.seconds 8.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                Expect.equal c.TotalDuration.TotalSeconds 16.0 "Unexpected duration"

                // 0.0
                Animator.createAndStart "Test" c ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, (0.0, 0.0)
                    EventType.Progress, (0.0, 0.0)
                ]

                // 2.0
                Animator.tickSeconds 2.0

                Expect.checkEvents eventsA [
                    EventType.Progress, 1.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.5
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (1.0, 0.5)
                ]

                // 4.0
                Animator.tickSeconds 4.0

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 1.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 1.0)
                ]

                // 6.0
                Animator.tickSeconds 6.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.5
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 0.5)
                ]

                // 8.0
                Animator.tickSeconds 8.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 1.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 1.0)
                ]

                // 9.0
                Animator.tickSeconds 9.0

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 0.75)
                ]

                // 13.0
                Animator.tickSeconds 13.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.0
                    EventType.Progress, 0.0
                    EventType.Progress, 0.5
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.75
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.5, 0.75)
                ]

                // 16.0
                Animator.tickSeconds 16.0

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 0.0)
                    EventType.Finalize, (0.0, 0.0)
                ]
            }

        let positionWithEasing =
            test "Position (with easing)" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Repeat 2
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.map2 (fun x y -> (x, y)) a b
                    |> Animation.easeCustom false ((+) 0.1 >> saturate)
                    |> Animation.trackEvents

                let instance =
                    Animator.createAndStart "Test" c ()
                    Animator.getUntyped "Test" ()

                // 0.0
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.2
                    EventType.Progress, 0.2
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.2
                    EventType.Progress, 0.2
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, (0.2, 0.2)
                    EventType.Progress, (0.2, 0.2)
                ]

                Expect.equal instance.Position LocalTime.zero "Unexpected position"

                // 1.3
                Animator.tickSeconds 1.3

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.5
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.5
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.5, 0.5)
                ]

                Expect.equal instance.Position (LocalTime.ofSeconds 1.3) "Unexpected position"

                // 2.1
                Animator.tickSeconds 2.1

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.0
                    EventType.Finalize, 0.0
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 1.0
                    EventType.Finalize, 1.0
                ]

                Expect.checkEvents eventsC [
                    EventType.Progress, (0.0, 1.0)
                    EventType.Finalize, (0.0, 1.0)
                ]

                Expect.equal instance.Position (LocalTime.ofSeconds 2.0) "Unexpected position"
            }

        let pauseResumeWithEasing =
            test "Pause / Resume (with easing)" {
                use _ = Animator.initTest()

                let eventsA, a =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Mirror 2
                    |> Animation.trackEvents

                let eventsB, b =
                    Animation.create id
                    |> Animation.seconds 1.0
                    |> Animation.loopN LoopMode.Repeat 2
                    |> Animation.trackEvents

                let eventsC, c =
                    Animation.map2 (fun x y -> (x, y)) a b
                    |> Animation.easeCustom false ((+) 0.1 >> saturate)
                    |> Animation.trackEvents

                Animator.createAndStart "Test" c ()
                Animator.tickSeconds 0.0

                Expect.checkEvents eventsA [
                    EventType.Start, 0.2
                    EventType.Progress, 0.2
                ]

                Expect.checkEvents eventsB [
                    EventType.Start, 0.2
                    EventType.Progress, 0.2
                ]

                Expect.checkEvents eventsC [
                    EventType.Start, (0.2, 0.2)
                    EventType.Progress, (0.2, 0.2)
                ]

                Animator.pause "Test" ()
                Animator.tickSeconds 0.4

                Expect.checkEvents eventsA [
                ]

                Expect.checkEvents eventsB [
                ]

                Expect.checkEvents eventsC [
                    EventType.Pause, (0.2, 0.2)
                ]

                Animator.resume "Test" ()
                Animator.tickSeconds 10.0

                Expect.checkEvents eventsA [
                    EventType.Progress, 0.6
                ]

                Expect.checkEvents eventsB [
                    EventType.Progress, 0.6
                ]

                Expect.checkEvents eventsC [
                    EventType.Resume, (0.2, 0.2)
                    EventType.Progress, (0.6, 0.6)
                ]
            }

    module private ZeroDuration =

        let private timed offset seconds : IAnimation<unit, float> =
            Animation.create ((+) offset) |> Animation.seconds seconds

        let private run (animation : IAnimation<unit, float>) start expected =
            use _ = Animator.initTest()
            let mutable completed = 0
            let animation = animation |> Animation.onFinalize (fun _ _ model -> completed <- completed + 1; model)
            let duration = animation.Duration.TotalSeconds
            let remaining = duration - start
            Animator.createAndStartFromLocal "Zero" animation (LocalTime.ofSeconds start) ()
            let instance : IAnimationInstance<unit, float> = Animator.get "Zero" ()
            for tick, position in [0.0, start; remaining / 2.0, (start + duration) / 2.0; remaining, duration] do
                Animator.tickSeconds tick
                Expect.floatClose Accuracy.high instance.Value (expected position) $"Value at local position {position}"
                Expect.equal instance.Position (LocalTime.ofSeconds position) "Group position"
                Expect.equal instance.IsFinished (position = duration) "Completion state"
            for tick in [remaining; remaining + 0.25; remaining + 1.0] do
                Animator.tickSeconds tick
                Expect.floatClose Accuracy.high instance.Value (expected duration) "Terminal value remains stable"
            Expect.equal completed 1 "Parent completes exactly once"

        let numeric kind start =
            let events, constant = Animation.Primitives.constant 42.0 |> Animation.trackEvents
            Expect.isTrue constant.Duration.IsZero "The constant keeps its zero duration"
            let a, b = timed 0.0 1.0, timed 10.0 1.0
            let group, initial, slope =
                match kind with
                | 0 -> Animation.map2 (fun x y -> x + 10.0 * y) constant a, 42.0, 10.0
                | 1 -> Animation.map2 (fun x y -> x + 10.0 * y) a constant, 420.0, 1.0
                | 2 -> Animation.map3 (fun x y z -> x + 10.0 * y + 100.0 * z) constant a b, 1042.0, 110.0
                | 3 -> Animation.map3 (fun x y z -> x + 10.0 * y + 100.0 * z) a constant b, 1420.0, 101.0
                | _ -> Animation.map3 (fun x y z -> x + 10.0 * y + 100.0 * z) a b constant, 4300.0, 11.0
            run group start (fun position -> initial + slope * position)
            Expect.checkEvents events [EventType.Start, 42.0; EventType.Progress, 42.0; EventType.Finalize, 42.0]

        let reference constantFirst =
            let text = "anchor"
            let constant = Animation.Primitives.constant text
            let mapping (value : string) position =
                Expect.isTrue (obj.ReferenceEquals(value, text)) "The mapper receives the original reference"
                float value.Length + 10.0 * position
            let group =
                if constantFirst then Animation.map2 mapping constant (timed 0.0 1.0)
                else Animation.map2 (fun position value -> mapping value position) (timed 0.0 1.0) constant
            run group 0.25 (fun position -> 6.0 + 10.0 * position)

        let nested () =
            let starts, completed = ResizeArray<string>(), ResizeArray<string>()
            let track name (animation : IAnimation<unit, float>) =
                animation
                |> Animation.onStart (fun _ _ model -> starts.Add name; model)
                |> Animation.onFinalize (fun _ _ model -> completed.Add name; model)
            let a = Animation.Primitives.constant 20.0 |> track "A"
            let b = Animation.Primitives.constant 22.0 |> track "B"
            let inner = Animation.map2 (+) a b |> track "Inner"
            let c = Animation.Primitives.constant 100.0 |> track "C"
            let zero = Animation.map2 (+) inner c |> track "Zero"
            Expect.isTrue zero.Duration.IsZero "Nested zero-duration groups remain zero-duration"
            let group = Animation.map2 (+) zero (timed 0.0 1.0 |> track "Timed") |> track "Parent"
            run group 0.0 (fun position -> 142.0 + position)
            Expect.checkEvents starts ["A"; "B"; "Inner"; "C"; "Zero"; "Timed"; "Parent"]
            Expect.checkEvents completed ["A"; "B"; "Inner"; "C"; "Zero"; "Timed"; "Parent"]

        let modelEffects () =
            use _ = Animator.initTest()
            let animator = ref Unchecked.defaultof<Animator<int>>
            animator.Value <- Animator.initial ((fun _ -> animator.Value), fun value model -> animator.Value <- value; model)
            let trace = ResizeArray<string * float * int>()
            let constant : IAnimation<int, float> =
                Animation.Primitives.constant 42.0
                |> Animation.onStart (fun _ value model -> trace.Add("ChildStart", value, model); model + 1)
                |> Animation.onFinalize (fun _ value model -> trace.Add("ChildFinalize", value, model); model + 3)
            let timed = Animation.create id |> Animation.seconds 1.0
            let group =
                Animation.map2' (fun model x y -> float model + x + y) constant timed
                |> Animation.onStart (fun _ value model -> trace.Add("ParentStart", value, model); model * 10)
                |> Animation.onProgress (fun _ value model -> trace.Add("ParentProgress", value, model); model * 100)
            let model = Animator.createAndStart "ZeroEffects" group 1 |> Animator.tick GlobalTime.zero
            Expect.equal model 2300 "Child effects reach the parent and returned model in order"
            Expect.checkEvents trace ["ChildStart", 42.0, 1; "ParentStart", 44.0, 2; "ChildFinalize", 42.0, 20; "ParentProgress", 65.0, 23]
            let instance : IAnimationInstance<int, float> = Animator.get "ZeroEffects" model
            Expect.equal instance.Value 65.0 "Parent evaluation sees the finalized child's effect"

        let restart () =
            use _ = Animator.initTest()
            let mutable starts, completed, parentCompleted = 0, 0, 0
            let constant =
                Animation.Primitives.constant 42.0
                |> Animation.onStart (fun _ _ model -> starts <- starts + 1; model)
                |> Animation.onFinalize (fun _ _ model -> completed <- completed + 1; model)
            let group =
                Animation.map2 (+) constant (timed 0.0 1.0)
                |> Animation.onFinalize (fun _ _ model -> parentCompleted <- parentCompleted + 1; model)
            Animator.createAndStart "ZeroRestart" group ()
            let instance : IAnimationInstance<unit, float> = Animator.get "ZeroRestart" ()
            Animator.tickSeconds 0.0
            Expect.equal instance.Value 42.0 "Initial value"
            Animator.tickSeconds 0.5
            Expect.equal instance.Value 42.5 "Progress before Stop"
            Animator.stop "ZeroRestart" ()
            Animator.tickSeconds 0.75
            Expect.isTrue instance.IsStopped "Stop resets the group"
            Expect.equal instance.Position LocalTime.zero "Stopped position"
            Expect.equal instance.Value 42.0 "Stopped value retains the constant"
            Animator.tickSeconds 1.0
            Expect.equal (starts, completed, parentCompleted) (1, 1, 0) "No duplicate completion while stopped"
            for tick, start in [2.0, 0.25; 5.0, 0.5] do
                Animator.restartFromLocal "ZeroRestart" (LocalTime.ofSeconds start) ()
                Animator.tickSeconds tick
                Expect.equal instance.Value (42.0 + start) "Restart from a nonzero position"
                Animator.tickSeconds (tick + 1.0 - start)
                Expect.isTrue instance.IsFinished "Restart completes"
                Expect.equal instance.Value 43.0 "Restart terminal value"
            for tick in [5.5; 6.0; 7.0] do Animator.tickSeconds tick
            Expect.equal (starts, completed, parentCompleted) (3, 3, 2) "One child completion per start and one parent completion per finished run"

        let controls kind =
            let animation, expected =
                match kind with
                | 0 -> Animation.Primitives.constant 42.0, fun _ -> 42.0
                | 1 -> Animation.map2 (+) (Animation.Primitives.constant 20.0) (Animation.Primitives.constant 22.0), fun _ -> 42.0
                | _ -> Animation.map2 (+) (timed 0.0 1.0) (timed 0.0 2.0), fun time -> min 1.0 time + time / 2.0
            run animation 0.0 expected

        let sequential () =
            let firstEvents, first = Animation.Primitives.constant 42.0 |> Animation.trackEvents
            let middleEvents, middle = Animation.Primitives.constant 99.0 |> Animation.trackEvents
            let group = Animation.sequential [first; timed 0.0 1.0; middle; timed 0.0 2.0]
            run group 0.0 (fun time -> if time < 1.0 then time else (time - 1.0) / 2.0)
            Expect.checkEvents firstEvents []
            Expect.checkEvents middleEvents []

    module private Stop =

        let private create concurrent nested =
            let stopped = ResizeArray<string * float>()
            let track name (animation : IAnimation<unit, float>) =
                animation |> Animation.onStop (fun _ value model ->
                    stopped.Add(name, value)
                    model)
            let leaf name offset duration =
                Animation.create ((+) offset) |> Animation.seconds duration |> track name
            let a = leaf "A" 0.0 1.0
            let b = leaf "B" 10.0 (if concurrent then 2.0 else 1.0)
            let inner = if concurrent then Animation.map2 (+) a b else Animation.sequential [a; b]
            let group =
                if not nested then inner
                else
                    let inner = inner |> track "Inner"
                    let c = leaf "C" 20.0 (if concurrent then 1.0 else 2.0)
                    if concurrent then Animation.sequential [inner; c] else Animation.map2 (+) inner c
            group |> track "Parent", stopped

        let check concurrent nested phase =
            use _ = Animator.initTest()
            let group, stopped = create concurrent nested
            Animator.createAndStart "Stop" group ()
            Animator.tickSeconds 0.0
            let instance : IAnimationInstance<unit, float> = Animator.get "Stop" ()
            let finished = phase = 2
            let time = if finished then group.Duration.TotalSeconds else 0.5
            Animator.tickSeconds time
            if phase = 1 then
                Animator.pause "Stop" ()
                Animator.tickSeconds 0.75
                Expect.isTrue instance.IsPaused "Paused setup"
            elif finished then Expect.isTrue instance.IsFinished "Finished setup"
            else Expect.isTrue instance.IsRunning "Running setup"
            Expect.equal instance.Position (LocalTime.ofSeconds time) "Position before Stop"
            stopped.Clear()

            Animator.stop "Stop" ()
            Animator.tickSeconds (group.Duration.TotalSeconds + 0.5)
            let reset = if concurrent then 10.0 elif nested then 20.0 else 0.0
            let expected = [
                "A", 0.0
                if concurrent || finished then "B", 10.0
                if nested then "Inner", (if concurrent then 10.0 else 0.0)
                if nested && (not concurrent || finished) then "C", 20.0
                "Parent", reset
            ]
            Expect.isTrue instance.IsStopped "Group is stopped"
            Expect.equal instance.Position LocalTime.zero "Group position resets"
            Expect.equal instance.Value reset "Group value resets with its children"
            Expect.checkEvents stopped expected

        let idempotenceAndRestart concurrent =
            use _ = Animator.initTest()
            let group, stopped = create concurrent false
            Animator.create "Stop" group ()
            let instance : IAnimationInstance<unit, float> = Animator.get "Stop" ()
            Animator.stop "Stop" ()
            Animator.tickSeconds 0.0
            Expect.checkEvents stopped []
            Expect.isTrue instance.IsStopped "Never-started group remains stopped"
            Expect.equal instance.Position LocalTime.zero "Never-started position"
            Expect.equal instance.Value 0.0 "Stop does not initialize never-started children"

            Animator.start "Stop" ()
            Animator.tickSeconds 1.0
            Animator.tickSeconds 1.5
            Expect.equal instance.Value (if concurrent then 10.75 else 0.5) "Ordinary progress"
            Animator.stop "Stop" ()
            Animator.tickSeconds 1.75
            if not concurrent then
                Expect.isFalse (stopped |> Seq.exists (fun (name, _) -> name = "B")) "Unreached child has no Stop callback"
            stopped.Clear()
            for time in [2.0; 2.25; 2.5] do
                Animator.stop "Stop" ()
                Animator.tickSeconds time
                Expect.checkEvents stopped []
                Expect.equal instance.Position LocalTime.zero "Repeated Stop position"

            Animator.start "Stop" ()
            Animator.tickSeconds 5.0
            Expect.equal instance.Value (if concurrent then 10.0 else 0.0) "Restart begins at the reset value"
            Expect.equal instance.Position LocalTime.zero "Restart begins at zero"
            Animator.tickSeconds 5.5
            Expect.equal instance.Value (if concurrent then 10.75 else 0.5) "Progress after restart"
            Expect.checkEvents stopped []

        let modelEffects concurrent =
            use _ = Animator.initTest()
            // Register the same external-state lens pattern as the unit Animator helper.
            let animator = ref Unchecked.defaultof<Animator<int>>
            animator.Value <- Animator.initial ((fun _ -> animator.Value), fun a model -> animator.Value <- a; model)
            let stopped = ResizeArray<string * float * int>()
            let track name transform (animation : IAnimation<int, float>) =
                animation |> Animation.onStop (fun _ value model ->
                    stopped.Add(name, value, model)
                    transform model)
            let a = Animation.create id |> Animation.seconds 1.0 |> track "A" ((+) 1)
            let b = Animation.create ((+) 10.0) |> Animation.seconds 1.0 |> track "B" ((*) 10)
            let group =
                if concurrent then Animation.map2' (fun model x y -> float model + x + y) a b
                else Animation.sequential [a; b]
            let group = group |> track "Parent" ((+) 100)
            let mutable model = Animator.createAndStart "Effects" group 1
            model <- Animator.tick GlobalTime.zero model
            let time = if concurrent then 0.5 else 1.5
            model <- Animator.tick (GlobalTime.ofNanoseconds (int64 (time * 1e9))) model
            model <- Animator.stop "Effects" model
            model <- Animator.tick (GlobalTime.ofNanoseconds (int64 ((time + 0.25) * 1e9))) model
            Expect.equal model 120 "Child Stop model effects compose before the parent"
            let reset = if concurrent then 30.0 else 0.0
            Expect.checkEvents stopped ["A", 0.0, 1; "B", 10.0, 2; "Parent", reset, 20]
            let instance : IAnimationInstance<int, float> = Animator.get "Effects" model
            Expect.equal instance.Value reset "Parent evaluation sees the children's model effects"
            Expect.equal instance.Position LocalTime.zero "Model-dependent reset position"
            model <- Animator.stop "Effects" model
            model <- Animator.tick (GlobalTime.ofNanoseconds (int64 ((time + 0.5) * 1e9))) model
            Expect.equal model 120 "Repeated Stop has no model effects"
            Expect.checkEvents stopped []

        let empty (animation : IAnimation<unit, 'Value>) value =
            use _ = Animator.initTest()
            let events, animation = animation |> Animation.seconds 1.0 |> Animation.trackEvents
            Animator.createAndStart "EmptyStop" animation ()
            Animator.tickSeconds 0.0
            Expect.checkEvents events [EventType.Start, value; EventType.Progress, value]
            Animator.stop "EmptyStop" ()
            Animator.tickSeconds 0.5
            Expect.checkEvents events [EventType.Stop, value]
            let instance : IAnimationInstance<unit, 'Value> = Animator.get "EmptyStop" ()
            Expect.equal instance.Value value "Empty reset value"
            Expect.equal instance.Position LocalTime.zero "Empty reset position"
            Animator.stop "EmptyStop" ()
            Animator.tickSeconds 0.75
            Expect.checkEvents events []
            Animator.start "EmptyStop" ()
            Animator.tickSeconds 1.0
            Expect.checkEvents events [EventType.Start, value; EventType.Progress, value]

    module private Completion =

        let private starting value = [EventType.Start, value; EventType.Progress, value]
        let private finishing value = [EventType.Progress, value; EventType.Finalize, value]

        let private create concurrent durationB mirror =
            let finalized = ResizeArray<string>()
            let track name (animation: IAnimation<unit, float>) =
                let events, animation = Animation.trackEvents animation
                events, animation |> Animation.onEvent EventType.Finalize (fun _ _ model ->
                    finalized.Add name
                    model)

            let eventsA, a = Animation.create id |> Animation.seconds 1.0 |> track "A"
            let eventsB, b = Animation.create id |> Animation.seconds durationB |> track "B"
            let group = if concurrent then Animation.map2 (+) a b else Animation.sequential [a; b]
            let group = if mirror then group |> Animation.loopN LoopMode.Mirror 2 else group
            let eventsGroup, group = group |> track "Parent"
            Animator.createAndStart "Completion" group ()
            let instance : IAnimationInstance<unit, float> = Animator.get "Completion" ()

            let check (seconds: float) expectedA expectedB expectedGroup expectedFinalized =
                Animator.tickSeconds seconds
                match List.tryLast expectedGroup with
                | Some (_, value) ->
                    Expect.equal instance.Value value "Unexpected group value"
                    Expect.equal instance.IsFinished
                        (expectedGroup |> List.exists (fun (event, _) -> event = EventType.Finalize))
                        "Unexpected group completion state"
                | None -> ()
                Expect.checkEvents eventsA expectedA
                Expect.checkEvents eventsB expectedB
                Expect.checkEvents eventsGroup expectedGroup
                Expect.checkEvents finalized expectedFinalized

            let complete seconds value =
                Expect.isTrue instance.IsFinished "Parent must finish at its terminal tick"
                // Include the same timestamp: completion must not be emitted twice.
                for tick in [seconds; seconds + 1.0; seconds + 2.0] do
                    check tick [] [] [] []
                    Expect.equal instance.Value value "Later ticks must preserve the terminal value"

            check 0.0 (starting 0.0) (if concurrent then starting 0.0 else []) (starting 0.0) []
            check, complete

        let sequential durationB =
            use _ = Animator.initTest()
            let check, complete = create false durationB false
            let terminal = 1.0 + durationB
            let expectedB =
                if durationB = 0.0 then starting 1.0 @ [EventType.Finalize, 1.0]
                else starting 0.0 @ finishing 1.0
            check terminal (finishing 1.0) expectedB (finishing 1.0) ["A"; "B"; "Parent"]
            complete terminal 1.0

        let mirrored concurrent =
            use _ = Animator.initTest()
            let check, complete = create concurrent (if concurrent then 2.0 else 1.0) true
            let atTurn = if concurrent then 2.0 else 1.0
            let expectedB = if concurrent then [EventType.Progress, 1.0] else starting 0.0 @ [EventType.Progress, 1.0]
            check 2.0 (finishing 1.0) expectedB [EventType.Progress, atTurn] ["A"]
            check 4.0 (starting 1.0 @ finishing 0.0) (finishing 0.0) (finishing 0.0) ["A"; "B"; "Parent"]
            complete 4.0 0.0

        let nonFinalSequential transition =
            use _ = Animator.initTest()
            let check, complete = create false 1.0 false
            let value = transition - 1.0
            let expectedB =
                if transition = 1.0 then starting 0.0
                else starting 0.0 @ [EventType.Progress, value]
            check transition (finishing 1.0) expectedB [EventType.Progress, value] ["A"]
            check 2.0 [] (finishing 1.0) (finishing 1.0) ["B"; "Parent"]
            complete 2.0 1.0

        let intermediateReentry concurrent =
            use _ = Animator.initTest()
            let check, complete = create concurrent (if concurrent then 2.0 else 1.0) true
            let atTurn = if concurrent then 2.0 else 1.0
            let expectedB = if concurrent then [EventType.Progress, 1.0] else starting 0.0 @ [EventType.Progress, 1.0]
            check 2.0 (finishing 1.0) expectedB [EventType.Progress, atTurn] ["A"]
            let expectedB = if concurrent then [EventType.Progress, 0.25] else finishing 0.0
            check 3.5 (starting 1.0 @ [EventType.Progress, 0.5]) expectedB
                [EventType.Progress, if concurrent then 0.75 else 0.5] (if concurrent then [] else ["B"])
            check 4.0 (finishing 0.0) (if concurrent then finishing 0.0 else [])
                (finishing 0.0) (if concurrent then ["A"; "B"; "Parent"] else ["A"; "Parent"])
            complete 4.0 0.0

    [<Tests>]
    let tests =
        testList "Animation.Groups" [
            testList "Sequential" [
                Sequential.progress
                Sequential.startFrom
                Sequential.positionWithEasing
                Sequential.pauseResumeWithEasing
                Sequential.outOfBoundsEasing
                Sequential.negativeEasing LoopMode.Repeat
                Sequential.negativeEasing LoopMode.Mirror
                Sequential.negativeEasing LoopMode.Continue
                Sequential.empty
                Sequential.emptyMember
            ]

            testList "Concurrent" [
                Concurrent.progress
                Concurrent.positionWithEasing
                Concurrent.pauseResumeWithEasing
            ]

            testList "Zero duration" [
                test "Map2 and map3 initialize constants in every position and at nonzero starts" {
                    for kind in 0 .. 4 do
                        for start in [0.0; 0.25; 0.5] do ZeroDuration.numeric kind start
                }
                test "Reference constants reach the mapper in either position" {
                    for first in [false; true] do ZeroDuration.reference first
                }
                testCase "Nested groups initialize and complete children before parents" ZeroDuration.nested
                testCase "Child callback model effects reach parent evaluation" ZeroDuration.modelEffects
                testCase "Stop and restart complete each zero member once per start" ZeroDuration.restart
                test "Standalone constants, all-zero groups and positive-duration members retain behavior" {
                    for kind in 0 .. 2 do ZeroDuration.controls kind
                }
                testCase "Sequential groups still skip nonterminal zero-duration members" ZeroDuration.sequential
            ]

            testList "Stop" [
                test "Sequential resets children before the parent" {
                    for phase in [0; 1; 2] do Stop.check false false phase
                }
                test "Map2 resets children before the parent" {
                    for phase in [0; 1; 2] do Stop.check true false phase
                }
                test "Nested groups reset recursively before their parent" {
                    for concurrent in [false; true] do
                        for phase in [0; 1; 2] do Stop.check concurrent true phase
                }
                test "Never-started children and repeated Stop preserve restart" {
                    for concurrent in [false; true] do Stop.idempotenceAndRestart concurrent
                }
                test "Child model effects reach parent evaluation and callback" {
                    for concurrent in [false; true] do Stop.modelEffects concurrent
                }
                test "Empty groups retain Stop and restart behavior" {
                    Stop.empty (Animation.sequential []) 0.0
                    Stop.empty (Animation.concurrent []) ()
                }
            ]

            testList "Completion" [
                test "Sequential terminal activation finalizes at the target" {
                    for duration in [1.0; 0.0] do Completion.sequential duration
                }
                test "Mirrored terminal reactivation finalizes at the target" {
                    for concurrent in [false; true] do Completion.mirrored concurrent
                }
                test "Non-final activation waits for ordinary completion" {
                    for transition in [1.0; 1.5] do Completion.nonFinalSequential transition
                }
                test "Mirrored intermediate reentry keeps ordinary completion" {
                    for concurrent in [false; true] do Completion.intermediateReentry concurrent
                }
            ]
        ]