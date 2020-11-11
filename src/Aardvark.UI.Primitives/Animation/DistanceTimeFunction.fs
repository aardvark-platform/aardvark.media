namespace Aardvark.UI.Anewmation

open System
open Aardvark.Base

type private DistanceTimeFunction(dtf : Func<MicroTime, bool * float>) =

    static let defaultFunction = DistanceTimeFunction(fun _ -> false, 0.0)

    /// Returns a flag indicating if the animation has finished, and a position within [0, 1] depending on the given time stamp.
    member x.Invoke(globalTime : MicroTime) = dtf.Invoke(globalTime)

    /// Default distance-time function that does not progress.
    static member Default = defaultFunction

    interface IDistanceTimeFunction with
        member x.Invoke(globalTime) = x.Invoke(globalTime)

[<AutoOpen>]
module AnimationTimeExtensions =

    module Animation =

        /// Sets the distance-time function of the given animation.
        let distanceTimeFunction (dtf : MicroTime -> bool * float) (animation : IAnimation<'Model, 'Value>) =
            animation.UpdateDistanceTimeFunction(fun _ ->
                DistanceTimeFunction(Func<_,_> dtf) :> IDistanceTimeFunction
            ) :?> IAnimation<'Model, 'Value>

        /// Sets the duration of the given animation.
        let duration (t : MicroTime) (animation : IAnimation<'Model, 'Value>) =
            let dtf duration time =
                let s = (time / duration)
                s >= 1.0, s

            animation |> distanceTimeFunction (dtf t)

        /// Sets the duration (in seconds) of the given animation.
        let inline seconds (s : ^Seconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofSeconds s)

        /// Sets the duration (in milliseconds) of the given animation.
        let inline milliseconds (ms : ^Milliseconds) (animation : IAnimation<'Model, 'Value>) =
            animation |> duration (MicroTime.ofMilliseconds ms)
