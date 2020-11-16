namespace Aardvark.UI.Anewmation

open System
open Aardvark.Base

[<AutoOpen>]
module private DistanceTimeFunctionUtilities =

    let inline repeat (s : float) =
        s % 1.0

    let inline mirror (s : float) =
        let t = s % 1.0
        if int s % 2 = 0 then t else 1.0 - t

type private DistanceTimeFunction =
    {
        Function : Func<MicroTime, float>
        Easing : Func<float, float>
        Iterations : int
        Mode : LoopMode
    }

    member inline private x.Wrap(s : float) =
        match x.Mode with
        | LoopMode.Repeat -> repeat s
        | LoopMode.Mirror -> mirror s

    member inline private x.Final(s : float) =
        match x.Mode with
        | LoopMode.Repeat -> 1.0
        | LoopMode.Mirror -> mirror s

    /// Returns a flag indicating if the animation has finished, and a position within [0, 1] depending
    /// on the time elapsed since the start of the animation.
    member x.Invoke(localTime : MicroTime) =
        let p = x.Function.Invoke(localTime)

        if x.Iterations > 0 && int p >= x.Iterations then
            true, x.Iterations |> float |> x.Final
        else
            false, p |> x.Wrap |> x.Easing.Invoke

    /// Applies an easing function, i.e. an function f: [0, 1] -> [0, 1] with f(0) = 0 and f(1) = 1.
    member x.Ease(easing) =
        { x with Easing = Func<_,_> (x.Easing.Invoke >> easing)}

    /// <summary>
    /// Sets the number of iterations and loop mode.
    /// </summary>
    /// <param name="iterations">The number of iterations or a nonpositive value for an unlimited number of iterations.</param>
    member x.Loop(iterations, mode) =
        { x with Iterations = iterations; Mode = mode }

    interface IDistanceTimeFunction with
        member x.Invoke(localTime) = x.Invoke(localTime)
        member x.Ease(easing) = x.Ease(easing) :> IDistanceTimeFunction
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IDistanceTimeFunction

module private DistanceTimeFunction =

    let Default =
        { Function = Func<_,_> (fun _ -> 0.0)
          Easing = Func<_,_> id
          Iterations = 1
          Mode = LoopMode.Repeat }

[<AutoOpen>]
module AnimationTimeExtensions =

    module Animation =

        /// Sets the distance-time function of the given animation.
        let distanceTimeFunction (dtf : MicroTime -> float) (animation : IAnimation<'Model, 'Value>) =
            animation.DistanceTime(fun _ ->
                { DistanceTimeFunction.Default with
                    Function = Func<_,_> dtf } :> IDistanceTimeFunction
            )

        /// Sets the duration of the given animation.
        let duration (t : MicroTime) (animation : IAnimation<'Model, 'Value>) =
            let dtf duration time =
                time / duration

            animation |> distanceTimeFunction (dtf t)

        /// Sets the duration (in seconds) of the given animation.
        let inline seconds (s : ^Seconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofSeconds s)

        /// Sets the duration (in milliseconds) of the given animation.
        let inline milliseconds (ms : ^Milliseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofMilliseconds ms)

        /// Sets the number of iterations and loop mode of the given animation.
        /// A count less than one results in an infinite number of iterations.
        let loop' (mode : LoopMode) (count : int) (animation : IAnimation<'Model, 'Value>) =
            animation.DistanceTime(fun dtf -> dtf.Loop(count, mode))

        /// Loops the given animation infinitely according to the given mode.
        let loop (mode : LoopMode) (animation : IAnimation<'Model, 'Value>) =
            animation |> loop' mode -1
