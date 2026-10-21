namespace Aardvark.UI.Tests

open System
open Aardvark.UI.Animation
open Expecto

module ``Group Direction Tests`` =

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
            let ns = int64 (Math.Round(phase * duration * 1E9))
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
        EasingFunction.Sine, (fun t -> if t = 1.0 then 1.0 else 1.0 - Math.Cos(t * Math.PI / 2.0)), [0.53, 0.59; 0.38, 0.46], [0.65, 0.70; 0.40, 0.50]
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

    [<Tests>]
    let tests =
        testList "Animation.GroupDirection" [
            for concurrent in [false; true] do
                test (if concurrent then "Map2 ease-in retains completed child values" else "Sequential ease-in uses forward callback endpoints") {
                    for easing, oracle, sequentialTimes, concurrentTimes in easeIns do
                        for index, first in List.indexed [0.5; 0.25] do
                            let before, after = (if concurrent then concurrentTimes else sequentialTimes).[index]
                            Expect.isTrue (before > oracle after) "Regression setup would reverse the mixed-domain comparison"
                            for scale in [1.0; 2.0; 4.0] do
                                forward concurrent first scale (Easing.In easing) oracle before after
                }
            test "Uneased and ease-out groups preserve boundary callbacks" {
                for concurrent in [false; true] do
                    for first in [0.5; 0.25] do
                        for scale in [1.0; 3.0] do
                            forward concurrent first scale Easing.None id 0.1 0.8
                            forward concurrent first scale (Easing.Out EasingFunction.Quadratic) (fun t -> 1.0 - (1.0 - t) * (1.0 - t)) 0.1 0.8
            }
            test "Mirrored groups retain genuinely backward boundary selection" {
                for concurrent in [false; true] do mirrored concurrent
            }
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
        ]
