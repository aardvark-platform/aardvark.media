namespace Aardvark.UI.Animation.Tests

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

    [<Tests>]
    let tests =
        testList "Groups" [
            testList "Sequential" [
                Sequential.progress
                Sequential.startFrom
                Sequential.positionWithEasing
                Sequential.pauseResumeWithEasing
            ]

            testList "Concurrent" [
                Concurrent.progress
            ]
        ]