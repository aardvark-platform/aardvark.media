namespace Aardvark.UI.Animation

open System
open Aardvark.Base

[<AutoOpen>]
module internal DistanceTimeFunctionUtilities =

    let repeat (s : float) =
        if s = 0.0 then
            0.0
        else
            let t = s - floor s
            if t = 0.0 then 1.0 else t

    let mirror (s : float) =
        let t = abs (s % 1.0)
        if int s % 2 = 0 then t else 1.0 - t

    let wrap (mode : LoopMode) (s : float) =
        match mode with
        | LoopMode.Repeat -> repeat s
        | LoopMode.Mirror -> mirror s
        | _ -> s


type internal DistanceTimeFunction =
    {
        Easing     : Func<float, float>
        Iterations : float
        Mode       : LoopMode
    }

    /// Returns the normalized distance along the space curve based on the given local time stamp.
    member this.Invoke(t : float) =
        if not <| isFinite t then
            Log.warn "[Animation] Distance-time function invoked with %f" t

        this.Easing.Invoke(t |> wrap this.Mode)

    /// <summary>
    /// Applies an easing function, i.e. a function f: s -> s on the normalized distance s where f(0) = 0 and f(1) = 1.
    /// </summary>
    /// <param name="easing">The easing function to apply.</param>
    /// <param name="compose">Indicates whether easing is composed or overwritten.</param>
    member this.Ease(easing, compose) =
        { this with Easing = Func<_,_> (if compose then this.Easing.Invoke >> easing else easing) }

    /// <summary>
    /// Sets the number of iterations and loop mode.
    /// </summary>
    /// <param name="iterations">The number of iterations or a non-positive value for an unlimited number of iterations.</param>
    /// <param name="mode">The loop or wrap mode.</param>
    member this.Loop(iterations, mode) =
        let iterations = if iterations > 0 then float iterations else infinity
        { this with Iterations = iterations; Mode = mode }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal DistanceTimeFunction =

    let empty =
        { Easing     = Func<_,_> id
          Iterations = 1.0
          Mode       = LoopMode.Continue }


[<AutoOpen>]
module AnimationTimeExtensions =

    module Animation =

        /// Sets the duration of the given animation.
        let duration (t : Duration) (animation : IAnimation<'Model, 'Value>) =
            animation.Scale t

        /// Sets the duration (in minutes) of the given animation.
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

        /// Loops the given animation infinitely according to the given mode.
        let loop (mode : LoopMode) (animation : IAnimation<'Model, 'Value>) =
            animation.Loop(0, mode)

        /// Sets the number of iterations (non-positive for unlimited iterations) and loop mode of the given animation.
        let loopN (mode : LoopMode) (count : int) (animation : IAnimation<'Model, 'Value>) =
            animation.Loop(count, mode)