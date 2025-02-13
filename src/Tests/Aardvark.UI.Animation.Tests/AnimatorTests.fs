namespace Aardvark.UI.Animation.Tests

open Aardvark.UI.Animation
open Expecto

module ``Animator Tests`` =

    module Processing =

        let animationStartedDuringTickIsUpdated =
            test "Animation started during tick is updated" {
                use _ = Animator.initTest()

                let started = ref false
                let updated = ref false

                let a1 =
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun name _ model ->
                        model |> Animator.createAndStart "a1" a1
                    )

                Animator.createAndStart "Test" a2 ()
                Animator.tickSeconds 1.0

                Expect.isTrue started.Value "Animation not started"
                Expect.isTrue updated.Value "Animation not updated"     
            }

    module Querying =

        let slotExistsInProgressCallback =
            test "Slot exists in onProgress callback" {
                use _ = Animator.initTest()

                let animation =
                    Animation.create (fun t -> t)
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
                    Animation.create (fun t -> t)
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun name _ model ->
                        Expect.isTrue (model |> Animator.exists name) "Exists returns false"
                        model |> Animator.getUntyped name |> ignore
                        model
                    )

                let a2 =
                    Animation.create (fun t -> t)
                    |> Animation.seconds 4.0
                    |> Animation.onStart(fun name _ model ->
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
        testList "Animator" [
            testList "Processing" [
                Processing.animationStartedDuringTickIsUpdated
            ]

            testList "Querying" [
                Querying.slotExistsInProgressCallback
                Querying.slotExistsWhenStartedDuringTick false
                Querying.slotExistsWhenStartedDuringTick true
            ]
        ]