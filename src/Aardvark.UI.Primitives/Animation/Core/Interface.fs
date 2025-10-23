namespace Aardvark.UI.Animation

open Aardvark.Base

/// Event types for animation instances.
[<RequireQualifiedAccess>]
type EventType =
    /// The animation instance was started or restarted.
    | Start

    /// The animation instance was resumed after being paused.
    | Resume

    /// The animation instance was updated.
    | Progress

    /// The animation instance was paused.
    | Pause

    /// The animation instance was stopped manually.
    | Stop

    /// The animation instance has finished.
    | Finalize

/// Actions that can be performed on animation instances.
[<RequireQualifiedAccess>]
type Action =
    /// Stop the animation instance and reset it.
    | Stop

    /// Start the animation instance from the given start time.
    | Start of startFrom: LocalTime

    /// Pause the animation instance if it is running.
    | Pause

    /// Resume the animation instance from the point it was paused.
    | Resume

    /// Update the animation instance to the given local time.
    | Update of time: LocalTime * finalize: bool


/// The state of an animation.
[<RequireQualifiedAccess>]
type State =
    /// The animation is running.
    | Running of startTime: GlobalTime

    /// The animation has not started yet or was stopped manually.
    | Stopped

    /// The animation has finished.
    | Finished

    /// The animation is paused at the given global time stamp.
    | Paused of startTime: GlobalTime * pauseTime: GlobalTime


/// Loop mode for animation iterations.
[<RequireQualifiedAccess>]
type LoopMode =
    /// Animation restarts at the beginning.
    | Repeat

    /// Animation is reversed upon reaching the end.
    | Mirror

type IAnimation =

    /// Returns the duration (per iteration) of the animation.
    abstract member Duration : Duration

    /// Returns the total duration of the animation.
    abstract member TotalDuration : Duration

    /// Returns the normalized distance along the space curve based on the given local time stamp.
    abstract member DistanceTime : LocalTime -> float


type IAnimationInstance<'Model> =
    inherit IAnimation

    /// The name of the animation instance.
    abstract member Name : Symbol

    /// Returns the current state of the animation instance.
    abstract member State : State

    /// Returns the current position of the animation instance.
    abstract member Position : LocalTime

    /// Returns whether the animation instance is out of date, i.e. whether there are uncommitted actions.
    abstract member OutOfDate : bool

    /// Performs the given action.
    abstract member Perform : Action -> unit

    /// Commits the animation instance, i.e. processes all actions performed since the last commit
    /// and notifies all observers of triggered events, invoking the respective callbacks.
    /// Returns the model computed by the callbacks.
    abstract member Commit : model: 'Model * tick: GlobalTime -> 'Model

    /// Returns the animation definition of this instance.
    abstract member Definition : IAnimation<'Model>

and IAnimationInstance<'Model, 'Value> =
    inherit IAnimationInstance<'Model>

    /// Returns the current value of the animation instance.
    abstract member Value : 'Value

    /// Returns the animation definition of this instance.
    abstract member Definition : IAnimation<'Model, 'Value>


and IAnimation<'Model> =
    inherit IAnimation

    /// Creates an animation instance with the given name.
    abstract member Create : name: Symbol -> IAnimationInstance<'Model>

    /// Sets the duration (per iteration) of the animation.
    abstract member Scale : duration: Duration -> IAnimation<'Model>

    /// <summary>
    /// Applies an easing function, i.e. a function f: s -> s on the normalized distance s where f(0) = 0 and f(1) = 1.
    /// </summary>
    /// <param name="easing">The easing function to apply.</param>
    /// <param name="compose">Indicates whether easing is composed or overwritten.</param>
    abstract member Ease : easing: (float -> float) * compose: bool -> IAnimation<'Model>

    /// <summary>
    /// Sets the number of iterations and loop mode.
    /// </summary>
    /// <param name="iterations">The number of iterations or a non-positive value for an unlimited number of iterations.</param>
    /// <param name="mode">The loop or wrap mode.</param>
    abstract member Loop : iterations: int * mode: LoopMode -> IAnimation<'Model>

    /// Removes all callbacks.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model>

and IAnimation<'Model, 'Value> =
    inherit IAnimation<'Model>

    /// Creates an animation instance with the given name.
    abstract member Create : name: Symbol -> IAnimationInstance<'Model, 'Value>

    /// Sets the duration (per iteration) of the animation.
    abstract member Scale : duration: Duration -> IAnimation<'Model, 'Value>

    /// <summary>
    /// Applies an easing function, i.e. a function f: s -> s on the normalized distance s where f(0) = 0 and f(1) = 1.
    /// </summary>
    /// <param name="easing">The easing function to apply.</param>
    /// <param name="compose">Indicates whether easing is composed or overwritten.</param>
    abstract member Ease : easing: (float -> float) * compose: bool -> IAnimation<'Model, 'Value>

    /// <summary>
    /// Sets the number of iterations and loop mode.
    /// </summary>
    /// <param name="iterations">The number of iterations or a non-positive value for an unlimited number of iterations.</param>
    /// <param name="mode">The loop or wrap mode.</param>
    abstract member Loop : iterations: int * mode: LoopMode -> IAnimation<'Model, 'Value>

    /// Registers a new callback.
    abstract member Subscribe : event: EventType * callback: (Symbol -> 'Value -> 'Model -> 'Model) -> IAnimation<'Model, 'Value>

    /// Removes all callbacks.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model, 'Value>


[<AutoOpen>]
module IAnimationInstanceExtensions =

    type IAnimationInstance<'Model> with

        /// Returns whether the animation instance is running.
        member x.IsRunning = x.State |> function State.Running _ -> true | _ -> false

        /// Returns whether the animation instance is stopped.
        member x.IsStopped = x.State = State.Stopped

        /// Returns whether the animation instance is finished.
        member x.IsFinished = x.State = State.Finished

        /// Returns whether the animation instance is paused.
        member x.IsPaused = x.State |> function State.Paused _ -> true | _ -> false

        /// Stops the animation instance and resets it.
        member x.Stop() =
            x.Perform Action.Stop

        /// Starts the animation instance from the given start time.
        member x.Start(startFrom: LocalTime) =
            x.Perform <| Action.Start startFrom

        /// Starts the animation instance from the given normalized position.
        member x.Start(startFrom: float) =
            let lt = if startFrom = 0.0 then LocalTime.zero else startFrom |> LocalTime.ofNormalizedPosition x.Definition.Duration
            x.Perform <| Action.Start lt

        /// Starts the animation instance from the beginning.
        member x.Start() =
            x.Perform <| Action.Start LocalTime.zero

        /// Pauses the animation instance if it is running or has started.
        member x.Pause() =
            x.Perform <| Action.Pause

        /// Resumes the animation instance from the point it was paused.
        /// Has no effect if the animation instance is not paused.
        member x.Resume() =
            x.Perform <| Action.Resume

        /// Updates the animation instance to the given local time.
        member x.Update(time: LocalTime, finalize: bool) =
            x.Perform <| Action.Update(time, finalize)