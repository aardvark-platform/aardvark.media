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

    module private Direction =

        type private Model =
            { Animator : Animator<Model>
              Events : (string * EventType * float list) list
              A : float option
              B : float option }

        let private initial () =
            { Animator = Animator.initial ((fun m -> m.Animator), fun a m -> { m with Animator = a })
              Events = []; A = None; B = None }

        let private watch name values (animation : IAnimation<Model, 'Value>) =
            (animation, [EventType.Start; EventType.Progress; EventType.Finalize; EventType.Stop; EventType.Pause; EventType.Resume])
            ||> List.fold (fun animation kind ->
                animation |> Animation.onEvent kind (fun _ value model ->
                    let values = values value
                    { model with
                        Events = (name, kind, values) :: model.Events
                        A = if name = "A" then Some (List.exactlyOne values) else model.A
                        B = if name = "B" then Some (List.exactlyOne values) else model.B }))

        let private start name values = name, EventType.Start, values
        let private progress name values = name, EventType.Progress, values
        let private finish name values = [progress name values; name, EventType.Finalize, values]
        let private close actual expected context =
            Expect.isTrue (abs (actual - expected) < 1E-8) $"{context}: {actual} != {expected}"
        let private closeValues actual expected context =
            Expect.equal (List.length actual) (List.length expected) context
            List.iter2 (fun a e -> close a e context) actual expected

        let private create concurrent first scale easing mirror =
            let a = Animation.create id |> Animation.seconds first |> watch "A" List.singleton
            let b = Animation.create id |> Animation.seconds 1.0 |> watch "B" List.singleton
            let group =
                if concurrent then Animation.map2 (fun a b -> [a; b]) a b
                else Animation.sequential [a; b] |> Animation.map List.singleton
            let duration = (if concurrent then 1.0 else first + 1.0) * scale
            let group = group |> Animation.seconds duration |> Animation.ease easing
            let group = if mirror then group |> Animation.loopN LoopMode.Mirror 2 else group
            let group = group |> watch "Parent" id
            let mutable model = Animator.createAndStart "Direction" group (initial ())
            let instance : IAnimationInstance<Model, float list> = Animator.get "Direction" model
            let check phase value events lastA lastB finished =
                let ns = int64 (round(phase * duration * 1E9))
                model <- Animator.tick (GlobalTime.ofNanoseconds ns) model
                Expect.equal instance.Position (LocalTime.ofNanoseconds ns) "Public Position remains raw group time"
                Expect.equal instance.IsFinished finished "Group completion state"
                closeValues instance.Value value "Retained group/child values"
                let actual = List.rev model.Events
                Expect.equal actual.Length (List.length events) "Complete callback count"
                List.iter2 (fun (name, kind, values) (expectedName, expectedKind, expectedValues) ->
                    Expect.equal (name, kind) (expectedName, expectedKind) "Child-before-parent callback order"
                    closeValues values expectedValues $"{name} {kind} value") actual events
                for actual, expected in [model.A, lastA; model.B, lastB] do
                    match actual, expected with
                    | Some a, Some e -> close a e "Retained callback model effect"
                    | _ -> Expect.equal actual expected "Unreached child must not change the model"
                model <- { model with Events = [] }
            check

        let private forward concurrent first scale easing oracle before after =
            let check = create concurrent first scale easing false
            let fraction = if concurrent then first else first / (first + 1.0)
            let parent a b = if concurrent then [a; b] else [b]
            let a0 = oracle before / fraction
            let b0 = oracle before
            let b1 = if concurrent then oracle after else (oracle after - fraction) / (1.0 - fraction)
            Expect.isTrue (oracle before < fraction && oracle after > fraction) "Fixture crosses the first child boundary"
            let initial = [
                start "A" [0.0]
                if concurrent then start "B" [0.0]
                start "Parent" (if concurrent then [0.0; 0.0] else [0.0])
                progress "A" [0.0]
                if concurrent then progress "B" [0.0]
                progress "Parent" (if concurrent then [0.0; 0.0] else [0.0])
            ]
            check 0.0 (if concurrent then [0.0; 0.0] else [0.0]) initial (Some 0.0) (if concurrent then Some 0.0 else None) false
            check before (if concurrent then [a0; b0] else [a0]) [
                progress "A" [a0]
                if concurrent then progress "B" [b0]
                progress "Parent" (if concurrent then [a0; b0] else [a0])
            ] (Some a0) (if concurrent then Some b0 else None) false
            check after (parent 1.0 b1) [
                yield! finish "A" [1.0]
                if not concurrent then
                    start "B" [0.0]
                    progress "B" [0.0]
                progress "B" [b1]
                progress "Parent" (parent 1.0 b1)
            ] (Some 1.0) (Some b1) false
            let later = (after + 1.0) / 2.0
            let b2 = if concurrent then oracle later else (oracle later - fraction) / (1.0 - fraction)
            check later (parent 1.0 b2) [progress "B" [b2]; progress "Parent" (parent 1.0 b2)] (Some 1.0) (Some b2) false
            check 1.0 (parent 1.0 1.0) (finish "B" [1.0] @ finish "Parent" (parent 1.0 1.0)) (Some 1.0) (Some 1.0) true
            // Repeated terminal ticks must not emit duplicate callbacks or change retained values.
            check 1.0 (parent 1.0 1.0) [] (Some 1.0) (Some 1.0) true

        // Independent formulas and literal tick pairs; no calls to the group's DistanceTime oracle.
        let private easeIns = [
            // The first map2 case is the .5s/1s reproduction: .7 -> .8 yields [1; .64], then [1; 1].
            EasingFunction.Quadratic, (fun t -> t * t), [0.55, 0.65; 0.40, 0.50], [0.70, 0.80; 0.45, 0.55]
            EasingFunction.Cubic, (fun t -> t * t * t), [0.68, 0.75; 0.55, 0.65], [0.75, 0.85; 0.60, 0.70]
            EasingFunction.Sine, (fun t -> if t = 1.0 then 1.0 else 1.0 - cos(t * Constant.PiHalf)), [0.53, 0.59; 0.38, 0.46], [0.65, 0.70; 0.40, 0.50]
        ]

        let private mirrored concurrent =
            let check = create concurrent 0.5 2.0 (Easing.In EasingFunction.Quadratic) true
            let parent a b = if concurrent then [a; b] else [a]
            check 0.0 (if concurrent then [0.0; 0.0] else [0.0]) [
                start "A" [0.0]
                if concurrent then start "B" [0.0]
                start "Parent" (if concurrent then [0.0; 0.0] else [0.0])
                progress "A" [0.0]
                if concurrent then progress "B" [0.0]
                progress "Parent" (if concurrent then [0.0; 0.0] else [0.0])
            ] (Some 0.0) (if concurrent then Some 0.0 else None) false
            check 1.0 (if concurrent then [1.0; 1.0] else [1.0]) [
                yield! finish "A" [1.0]
                if not concurrent then
                    start "B" [0.0]
                    progress "B" [0.0]
                progress "B" [1.0]
                progress "Parent" (if concurrent then [1.0; 1.0] else [1.0])
            ] (Some 1.0) (Some 1.0) false
            // Raw time increases, but mirrored eased time genuinely moves backwards across the boundary.
            let a = if concurrent then 0.32 else 0.48
            let b = if concurrent then 0.16 else 0.0
            check 1.6 (parent a b) [
                start "A" [1.0]; progress "A" [1.0]; progress "A" [a]
                progress "B" [b]
                if not concurrent then "B", EventType.Finalize, [0.0]
                progress "Parent" (parent a b)
            ] (Some a) (Some b) false
            check 2.0 (if concurrent then [0.0; 0.0] else [0.0]) [
                yield! finish "A" [0.0]
                if concurrent then yield! finish "B" [0.0]
                yield! finish "Parent" (if concurrent then [0.0; 0.0] else [0.0])
            ] (Some 0.0) (Some 0.0) true

        let easeInForwardCallback (concurrent: bool) =
            let name =
                if concurrent then "Map2 ease-in retains completed child values"
                else "Sequential ease-in uses forward callback endpoints"

            test name {
                for easing, oracle, sequentialTimes, concurrentTimes in easeIns do
                    for index, first in List.indexed [0.5; 0.25] do
                        let before, after = (if concurrent then concurrentTimes else sequentialTimes).[index]
                        Expect.isTrue (before > oracle after) "Regression setup would reverse the mixed-domain comparison"
                        for scale in [1.0; 2.0; 4.0] do
                            forward concurrent first scale (Easing.In easing) oracle before after
            }

        let preserveBoundaryCallbacks =
            test "Uneased and ease-out groups preserve boundary callbacks" {
                for concurrent in [false; true] do
                    for first in [0.5; 0.25] do
                        for scale in [1.0; 3.0] do
                            forward concurrent first scale Easing.None id 0.1 0.8
                            forward concurrent first scale (Easing.Out EasingFunction.Quadratic) (fun t -> 1.0 - (1.0 - t) * (1.0 - t)) 0.1 0.8
            }

        let preserveMirroredBackwardBoundary =
            test "Mirrored groups retain genuinely backward boundary selection" {
                for concurrent in [false; true] do mirrored concurrent
            }

        let doNotReevaluatePriorEasing =
            test "Ordinary running updates do not reevaluate prior easing" {
                for concurrent in [false; true] do
                    let mutable calls = 0
                    let child : IAnimation<Model, float> = Animation.create id |> Animation.seconds 1.0
                    let group = if concurrent then Animation.map2 (+) child child else Animation.sequential [child]
                    let group = group |> Animation.easeCustom false (fun t -> calls <- calls + 1; t * t)
                    let mutable model = Animator.createAndStart "Counting" group (initial ())
                    model <- Animator.tick GlobalTime.zero model
                    calls <- 0
                    model <- Animator.tick (GlobalTime.ofSeconds 0.1) model
                    Expect.equal calls (if concurrent then 1 else 2) "Only current action/parent selection may evaluate easing"
                    let instance = Animator.getUntyped "Counting" model
                    Expect.equal instance.Position (LocalTime.ofSeconds 0.1) "Raw public position"
            }

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

            testList "Direction" [
                for concurrent in [false; true] do
                    Direction.easeInForwardCallback concurrent

                Direction.preserveBoundaryCallbacks
                Direction.preserveMirroredBackwardBoundary
                Direction.doNotReevaluatePriorEasing
            ]
        ]