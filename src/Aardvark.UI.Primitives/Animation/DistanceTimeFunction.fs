namespace Aardvark.UI.Anewmation

open System
open Aardvark.Base

[<AutoOpen>]
module private DistanceTimeFunctionUtilities =

    let inline repeat (s : float) =
        if s = 0.0 then
            0.0
        else
            let t = s % 1.0
            if t = 0.0 then 1.0 else t

    let inline mirror (s : float) =
        let t = s % 1.0
        if int s % 2 = 0 then t else 1.0 - t

    let inline wrap (mode : LoopMode) (s : float) =
        match mode with
        | LoopMode.Repeat -> repeat s
        | LoopMode.Mirror -> mirror s


type private DistanceTimeFunction =
    {
        Easing : Func<float, float>
        Iterations : Iterations
        Mode : LoopMode
    }

    /// Returns the normalized distance along the space curve based on the given local time stamp.
    member x.Invoke(t : float) =
        if not <| isFinite t then
            Log.warn "[Animation] Distance-time function invoked with %f" t

        let tmax = float x.Iterations

        if t < 0.0 || t > tmax then t
        else x.Easing.Invoke(t |> wrap x.Mode)

    /// <summary>
    /// Applies an easing function, i.e. a function f: s -> s on the normalized distance s where f(0) = 0 and f(1) = 1.
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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private DistanceTimeFunction =

    let empty =
        { Easing = Func<_,_> id
          Iterations = Iterations.Finite 1
          Mode = LoopMode.Repeat }


[<AutoOpen>]
module AnimationTimeExtensions =

    module Animation =

        /// Sets the duration of the given animation.
        let duration (t : Duration) (animation : IAnimation<'Model, 'Value>) =
            animation.Scale t

        /// Sets the duration (in ninutes) of the given animation.
        let inline minutes (m : ^Minutes) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (Duration.ofMinutes m)

        /// Sets the duration (in seconds) of the given animation.
        let inline seconds (s : ^Seconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (Duration.ofSeconds s)

        /// Sets the duration (in milliseconds) of the given animation.
        let inline milliseconds (ms : ^Milliseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (Duration.ofMilliseconds ms)

        /// Sets the duration (in microseconds) of the given animation.
        let inline microseconds (us : ^Microseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (Duration.ofMicroseconds us)

        /// Sets the duration (in nanoseconds) of the given animation.
        let inline nanoseconds (ns : ^Nanoseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (Duration.ofNanoseconds ns)

        /// Sets the number of iterations and loop mode of the given animation.
        let loop' (mode : LoopMode) (count : Iterations) (animation : IAnimation<'Model, 'Value>) =
            animation.Loop(count, mode)

        /// Loops the given animation infinitely according to the given mode.
        let loop (mode : LoopMode) (animation : IAnimation<'Model, 'Value>) =
            animation |> loop' mode Iterations.Infinite

        /// Sets the number of iterations (must be > 0) and loop mode of the given animation.
        let loopN (mode : LoopMode) (count : int) (animation : IAnimation<'Model, 'Value>) =
            animation |> loop' mode (Iterations.Finite count)