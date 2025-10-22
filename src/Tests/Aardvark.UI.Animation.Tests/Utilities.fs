namespace Aardvark.UI.Animation.Tests

open Aardvark.Base
open Aardvark.UI.Animation
open Expecto
open System
open System.Threading

module Animator =

    let private testLock = obj()

    let initTest() =
        let mutable lockTaken = false

        try
            Monitor.Enter(testLock, &lockTaken)

            let animator = ref Unchecked.defaultof<_>
            animator.Value <- Animator.initial ((fun () -> animator.Value), fun a () -> animator.Value <- a)

            { new IDisposable with member x.Dispose() = if lockTaken then Monitor.Exit testLock }
        with _ ->
            if lockTaken then Monitor.Exit(testLock)
            reraise()

    let inline tickSeconds (seconds: ^T) =
        let gt = GlobalTime <| MicroTime.ofSeconds seconds
        () |> Animator.update (AnimatorMessage.Tick gt)

module Animation =

    let trackEvents (animation: IAnimation<'Model, 'Value>) =
        let events = ResizeArray()
        let register t = Animation.onEvent t (fun _ v m -> events.Add(t, v); m)

        events,
        animation
        |> register EventType.Start
        |> register EventType.Resume
        |> register EventType.Progress
        |> register EventType.Pause
        |> register EventType.Stop
        |> register EventType.Finalize

module Expect =

    let checkEvents (actual: ResizeArray<'T>) (expected: 'T seq) =
        Expect.sequenceEqual actual expected "Unexpected events"
        actual.Clear()

    let v3dClose (accuracy: Accuracy) (actual: V3d) (expected: V3d) (message: string) =
        for i = 0 to 2 do Expect.floatClose accuracy actual.[i] expected.[i] message