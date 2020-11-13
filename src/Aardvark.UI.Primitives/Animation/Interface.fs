namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

/// Event types for animations.
/// Their values determine the order in which callbacks of different types are invoked.
[<RequireQualifiedAccess>]
type EventType =
    /// The animation was started or restarted.
    | Start = 0

    /// The animation was resumed after being paused.
    | Resume = 1

    /// The animation was updated.
    | Progress = 2

    /// The animation was paused.
    | Pause = 3

    /// The animation was stopped manually.
    | Stop = 4

    /// The animation has finished.
    | Finalize = 5

// Untyped interface for animation observers.
type IAnimationObserver<'Model> =

    /// Returns whether the observer does not have any callbacks.
    abstract member IsEmpty : bool

/// Interface for animation observers.
type IAnimationObserver<'Model, 'Value> =
    inherit IAnimationObserver<'Model>

    /// Adds a callback for the given event.
    abstract member Add : callback: (Symbol -> 'Value -> 'Model -> 'Model) * event: EventType -> IAnimationObserver<'Model, 'Value>

    /// Invoked on animation events.
    abstract member OnNext : model: 'Model * name: Symbol * event: EventType * value: 'Value -> 'Model


/// Interface for distance-time functions.
type IDistanceTimeFunction =

    /// Returns a flag indicating if the animation has finished, and a position within [0, 1] depending
    /// on the time elapsed since the start of the animation.
    abstract member Invoke : time: MicroTime -> bool * float


/// The state of an animation.
[<RequireQualifiedAccess>]
type State =
    /// The animation is running.
    | Running of startTime: MicroTime

    /// The animation has not started yet or was stopped manually.
    | Stopped

    /// The animation has finished.
    | Finished

    /// The animation is paused at the given global time stamp.
    | Paused of startTime: MicroTime * pauseTime: MicroTime

/// Untyped Interface for animations.
type IAnimation<'Model> =

    /// Returns the state of the animation.
    abstract member State : State

    /// Stops the animation and resets it.
    abstract member Stop : unit -> IAnimation<'Model>

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    abstract member Start : globalTime: MicroTime -> IAnimation<'Model>

    /// Pauses the animation if it is running or has started.
    abstract member Pause : globalTime: MicroTime -> IAnimation<'Model>

    /// Resumes the animation from the point it was paused.
    /// Has no effect if the animation is not paused.
    abstract member Resume : globalTime: MicroTime -> IAnimation<'Model>

    /// Updates the animation to the given global time.
    abstract member Update : globalTime: MicroTime -> IAnimation<'Model>

    /// Updates the distance-time function of the animation according to the given mapping.
    abstract member DistanceTime : (IDistanceTimeFunction -> IDistanceTimeFunction) -> IAnimation<'Model>

    /// Notifies all observers, invoking the respective callbacks.
    /// Returns the model computed by the callbacks.
    abstract member Notify : lens: Lens<'Model, IAnimation<'Model>> * name: Symbol * model: 'Model -> 'Model

    /// Removes the given observer (if present).
    abstract member Unsubscribe : observer: IAnimationObserver<'Model> -> IAnimation<'Model>

    /// Removes all observers.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model>


/// Interface for animations.
type IAnimation<'Model, 'Value> =
    inherit IAnimation<'Model>

    /// Returns the current value of the animation.
    abstract member Value : 'Value

    /// Registers a new observer.
    abstract member Subscribe : observer: IAnimationObserver<'Model, 'Value> -> IAnimation<'Model, 'Value>

    /// Removes the given observer (if present).
    abstract member Unsubscribe : observer: IAnimationObserver<'Model> -> IAnimation<'Model, 'Value>

    /// Removes all observers.
    abstract member UnsubscribeAll : unit -> IAnimation<'Model, 'Value>

    /// Stops the animation and resets it.
    abstract member Stop : unit -> IAnimation<'Model, 'Value>

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    abstract member Start : globalTime: MicroTime -> IAnimation<'Model, 'Value>

    /// Pauses the animation if it is running or has started.
    abstract member Pause : globalTime: MicroTime -> IAnimation<'Model, 'Value>

    /// Resumes the animation from the point it was paused.
    /// Has no effect if the animation is not paused.
    abstract member Resume : globalTime: MicroTime -> IAnimation<'Model, 'Value>

    /// Updates the animation to the given global time.
    abstract member Update : globalTime: MicroTime -> IAnimation<'Model, 'Value>

    /// Updates the distance-time function of the animation according to the given mapping.
    abstract member DistanceTime : (IDistanceTimeFunction -> IDistanceTimeFunction) -> IAnimation<'Model, 'Value>