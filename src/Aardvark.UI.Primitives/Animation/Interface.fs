namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

/// Event types for animations.
[<RequireQualifiedAccess>]
type EventType =
    /// The animation was started or restarted.
    | Start

    /// The animation was resumed after being paused.
    | Resume

    /// The animation was updated.
    | Progress

    /// The animation was paused.
    | Pause

    /// The animation was stopped manually.
    | Stop

    /// The animation has finished.
    | Finalize

/// Actions that can be performed on animations.
[<RequireQualifiedAccess>]
type Action =
    /// Stop the animation and reset it.
    | Stop

    /// Start the animation from the given start time (local timestamp relative to globalTime).
    | Start  of globalTime: GlobalTime * startFrom: LocalTime

    /// Pause the animation if it is running.
    | Pause  of globalTime: GlobalTime

    /// Resume the animation from the point it was paused.
    | Resume of globalTime: GlobalTime

    /// Update the animation to the given global time.
    | Update of globalTime: GlobalTime * finalize: bool


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

[<RequireQualifiedAccess>]
type Iterations =
    /// Finite number of iterations (must be > 0)
    | Finite of int

    /// Infinite number of iterations
    | Infinite

    static member Zero =
        Finite 0

    static member inline (*) (x : Iterations, y : Duration) =
        match x with
        | Finite n -> y * n
        | Infinite -> Duration.Infinite

    static member inline (*) (x : Duration, y : Iterations) =
        y * x

    static member inline op_Explicit (x : Iterations) : float =
        match x with
        | Finite n -> float n
        | Infinite -> infinity


/// Untyped Interface for animations.
type IAnimation<'Model> =

    /// Returns the state of the animation.
    abstract member State : State

    /// Returns the duration (per iteration) of the animation.
    abstract member Duration : Duration

    /// Returns the total duration of the animation.
    abstract member TotalDuration : Duration

    /// Returns the normalized distance along the space curve based on the given local time stamp.
    abstract member DistanceTime : LocalTime -> float

    /// Performs the given action.
    abstract member Perform : Action -> IAnimation<'Model>

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
    /// <param name="iterations">The number of iterations.</param>
    /// <param name="mode">The loop or wrap mode.</param>
    abstract member Loop : iterations: Iterations * mode: LoopMode -> IAnimation<'Model>

    /// Commits the animation, i.e. processes all actions performed since the last commit
    /// and notifies all observers of triggered events, invoking the respective callbacks.
    /// Returns the model computed by the callbacks.
    abstract member Commit : lens: Lens<'Model, IAnimation<'Model>> * name: Symbol * model: 'Model -> 'Model

    /// Removes all callbacks.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model>


/// Interface for animations.
type IAnimation<'Model, 'Value> =
    inherit IAnimation<'Model>

    /// Returns the current value of the animation.
    abstract member Value : 'Value

    /// Registers a new callback.
    abstract member Subscribe : event: EventType * callback: (Symbol -> 'Value -> 'Model -> 'Model) -> IAnimation<'Model, 'Value>

    /// Removes all observers.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model, 'Value>

    /// Performs the given action.
    abstract member Perform : Action -> IAnimation<'Model, 'Value>

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
    /// <param name="iterations">The number of iterations.</param>
    /// <param name="mode">The loop or wrap mode.</param>
    abstract member Loop : iterations: Iterations * mode: LoopMode -> IAnimation<'Model, 'Value>


[<AutoOpen>]
module IAnimationExtensions =

    type IAnimation<'Model> with

        /// Returns whether the animation is running.
        member x.IsRunning = x.State |> function State.Running _ -> true | _ -> false

        /// Returns whether the animation is stopped.
        member x.IsStopped = x.State = State.Stopped

        /// Returns whether the animation is finished.
        member x.IsFinished = x.State = State.Finished

        /// Returns whether the animation is paused.
        member x.IsPaused = x.State |> function State.Paused _ -> true | _ -> false

        /// Stops the animation and resets it.
        member x.Stop() =
            x.Perform Action.Stop

        /// Starts the animation from the given start time (local timestamp relative to globalTime).
        member x.Start(globalTime: GlobalTime, startFrom: LocalTime) =
            x.Perform <| Action.Start(globalTime, startFrom)

        /// Starts the animation from the given normalized position.
        member x.Start(globalTime: GlobalTime, startFrom: float) =
            let lt = if startFrom = 0.0 then LocalTime.zero else startFrom |> LocalTime.get x.Duration
            x.Perform <| Action.Start(globalTime, lt)

        /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
        member x.Start(globalTime: GlobalTime) =
            x.Perform <| Action.Start(globalTime, LocalTime.zero)

        /// Pauses the animation if it is running or has started.
        member x.Pause(globalTime: GlobalTime) =
            x.Perform <| Action.Pause(globalTime)

        /// Resumes the animation from the point it was paused.
        /// Has no effect if the animation is not paused.
        member x.Resume(globalTime: GlobalTime) =
            x.Perform <| Action.Resume(globalTime)

        /// Updates the animation to the given global time.
        member x.Update(globalTime: GlobalTime, finalize: bool) =
            x.Perform <| Action.Update(globalTime, finalize)


    type IAnimation<'Model, 'Value> with

        /// Stops the animation and resets it.
        member x.Stop() =
            x.Perform Action.Stop

        /// Starts the animation from the given start time (local timestamp relative to globalTime).
        member x.Start(globalTime: GlobalTime, startFrom: LocalTime) =
            x.Perform <| Action.Start(globalTime, startFrom)

        /// Starts the animation from the given normalized position.
        member x.Start(globalTime: GlobalTime, startFrom: float) =
            let lt = if startFrom = 0.0 then LocalTime.zero else startFrom |> LocalTime.get x.Duration
            x.Perform <| Action.Start(globalTime, lt)

        /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
        member x.Start(globalTime: GlobalTime) =
            x.Perform <| Action.Start(globalTime, LocalTime.zero)

        /// Pauses the animation if it is running or has started.
        member x.Pause(globalTime: GlobalTime) =
            x.Perform <| Action.Pause(globalTime)

        /// Resumes the animation from the point it was paused.
        /// Has no effect if the animation is not paused.
        member x.Resume(globalTime: GlobalTime) =
            x.Perform <| Action.Resume(globalTime)

        /// Updates the animation to the given global time.
        member x.Update(globalTime: GlobalTime, finalize: bool) =
            x.Perform <| Action.Update(globalTime, finalize)