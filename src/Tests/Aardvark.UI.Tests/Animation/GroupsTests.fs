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