namespace Aardvark.UI.Animation.Tests

open Aardvark.UI.Animation
open Expecto

module ``Simple Tests`` =

    module Events =
        let startFinish =
            test "Start / Finish" {
                use _ = Animator.initTest()

                let events, animation =
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
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

    [<Tests>]
    let tests =
        testList "Simple" [
            testList "Events" [
                Events.startFinish
                Events.pauseResume
                Events.pauseFinish
                Events.stopRestartFrom
                Events.stopFinish
            ]
        ]