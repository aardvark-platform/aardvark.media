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
        Duration : MicroTime
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
        let p = localTime / x.Duration

        if x.Duration.IsZero || (x.Iterations > 0 && int p >= x.Iterations) then
            true, x.Iterations |> float |> x.Final
        else
            false, p |> x.Wrap |> x.Easing.Invoke

    /// Sets the duration of the animation.
    member x.Scale(duration) =
        { x with Duration = duration }

    /// <summary>
    /// Applies an easing function, i.e. a function f: [0, 1] -> [0, 1] with f(0) = 0 and f(1) = 1.
    /// </summary>
    /// <param name="easing">The easing function to apply.</param>
    /// <param name="compose">Indicates whether easing is composed or overwritten.</param>
    member x.Ease(easing, compose) =
        { x with Easing = Func<_,_> (if compose then x.Easing.Invoke >> easing else easing) }

    /// <summary>
    /// Sets the number of iterations and loop mode.
    /// </summary>
    /// <param name="iterations">The number of iterations or a nonpositive value for an unlimited number of iterations.</param>
    member x.Loop(iterations, mode) =
        { x with Iterations = iterations; Mode = mode }


module private DistanceTimeFunction =

    let empty =
        { Duration = MicroTime.Zero
          Easing = Func<_,_> id
          Iterations = 1
          Mode = LoopMode.Repeat }

    let create (duration : MicroTime) =
        { empty with Duration = duration }


[<AutoOpen>]
module AnimationTimeExtensions =

    module Animation =

        /// Sets the duration of the given animation.
        let duration (t : MicroTime) (animation : IAnimation<'Model, 'Value>) =
            animation.Scale t

        /// Sets the duration (in seconds) of the given animation.
        let inline seconds (s : ^Seconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofSeconds s)

        /// Sets the duration (in milliseconds) of the given animation.
        let inline milliseconds (ms : ^Milliseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofMilliseconds ms)

        /// Sets the number of iterations and loop mode of the given animation.
        /// A count less than one results in an infinite number of iterations.
        let loop' (mode : LoopMode) (count : int) (animation : IAnimation<'Model, 'Value>) =
            animation.Loop(count, mode)

        /// Loops the given animation infinitely according to the given mode.
        let loop (mode : LoopMode) (animation : IAnimation<'Model, 'Value>) =
            animation |> loop' mode -1
