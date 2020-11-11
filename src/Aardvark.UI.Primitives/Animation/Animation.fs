namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open Aether

open Aardvark.UI.Anewmation

module private Events =

    /// Computes which events have to be triggered based on two animation states.
    let compute (prev : State) (current : State) =
        match prev, current with
        | State.Running _, State.Stopped
        | State.Paused _, State.Stopped         -> [EventType.Stop]
        | State.Stopped, State.Running _
        | State.Finished, State.Running _       -> [EventType.Start; EventType.Progress]
        | State.Running t1, State.Running t2    -> if t1 < t2 then [EventType.Start; EventType.Progress] else [EventType.Progress]
        | State.Running _, State.Paused _       -> [EventType.Pause]
        | State.Paused _, State.Running _       -> [EventType.Resume]
        | State.Running _, State.Finished       -> [EventType.Progress; EventType.Finalize]
        | _ -> []

/// An animation consists of a space function and a distance-time function.
/// The space function defines the animation values based on position (parameterized by arc length in the range of [0, 1]).
/// The distance-time function controls how the position changes over time.
type private Animation<'Model, 'Value> =
    {
        /// The next state of the animation.
        NextState : State

        /// The current state of the animation.
        CurrentState : State

        /// The current value of the animated value.
        CurrentValue : 'Value

        /// The distance along the curve defining the animation space (within [0, 1]).
        CurrentPosition : float

        /// Curve defining the animation values based on
        /// its position (parameterized by arc length in the range of [0, 1]).
        SpaceFunction : ISpaceFunction<'Value>

        /// Function controlling how the animation position changes over time.
        /// Returns a flag indicating if the animation has finished and the position.
        DistanceTimeFunction : IDistanceTimeFunction

        /// Observers to be notified of changes.
        Observers : HashSet<IObserver<'Model, 'Value>>
    }

    /// Stops the animation and resets it.
    member x.Stop() =
        { x with
            NextState = State.Stopped
            CurrentValue = x.SpaceFunction.Invoke 0.0
            CurrentPosition = 0.0 }

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    member x.Start(globalTime : MicroTime) =
        { x with
            NextState = State.Running globalTime
            CurrentValue = x.SpaceFunction.Invoke 0.0
            CurrentPosition = 0.0 }

    /// Pauses the animation if it is running or has started.
    member x.Pause(globalTime : MicroTime) =
        match x.CurrentState with
        | State.Running startTime ->
            { x with NextState = State.Paused(startTime, globalTime) }
        | _ ->
            x

    /// Resumes the animation from the point it was paused.
    /// Has no effect if the animation is not paused.
    member x.Resume(globalTime : MicroTime) =
        match x.CurrentState with
        | State.Paused (startTime, pauseTime) ->
            { x with NextState = State.Running (globalTime - (pauseTime - startTime)) }
        | _ ->
            x

    /// Updates the animation to the given global time.
    member x.Update(globalTime : MicroTime) =
        match x.CurrentState with
        | State.Running startTime ->
            let f, p = (globalTime - startTime) |> x.DistanceTimeFunction.Invoke
            let s = p |> clamp 0.0 1.0
            let v = x.SpaceFunction.Invoke s

            { x with
                NextState = if f then State.Finished else State.Running startTime
                CurrentValue = v
                CurrentPosition = s }
        | _ ->
            x

    /// Updates the distance time function of the animation.
    member x.UpdateDistanceTimeFunction(mapping : IDistanceTimeFunction -> IDistanceTimeFunction) =
        { x with DistanceTimeFunction = mapping x.DistanceTimeFunction }

    /// Registers a new observer.
    member x.Subscribe(observer : IObserver<'Model, 'Value>) =
        if not observer.IsEmpty then
            { x with Observers = x.Observers |> HashSet.add observer }
        else
            x

    /// Removes all observers.
    member x.UnsubscribeAll() =
        { x with Observers = HashSet.empty }

    /// Removes the given observer (if present).
    member x.Unsubscribe(observer : IObserver<'Model, 'Value>) =
        { x with Observers = x.Observers |> HashSet.remove observer }

    /// Notifies all observers.
    member x.Notify(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        let events = (x.CurrentState, x.NextState) ||> Events.compute
        let model = model |> Optic.set lens ({ x with CurrentState = x.NextState } :> IAnimation<'Model>)

        let notify model event =
            (model, x.Observers)
            ||> HashSet.fold (fun model obs -> obs.OnNext(model, name, event, x.CurrentValue))

        (model, events) ||> Seq.fold notify

    /// Updates the distance time function of the animation.
    member x.UpdateSpaceFunction(mapping : ISpaceFunction<'Value> -> ISpaceFunction<'Value>) =
        { x with SpaceFunction = mapping x.SpaceFunction }

    interface IAnimation<'Model, 'Value> with
        member x.State = x.CurrentState
        member x.Stop() = x.Stop() :> IAnimation<'Model>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model>
        member x.UpdateDistanceTimeFunction(f) = x.UpdateDistanceTimeFunction(f) :> IAnimation<'Model>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'Value>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>
        member x.Notify(lens, name, model) = x.Notify(lens, name, model)
        member x.UpdateSpaceFunction(f) = x.UpdateSpaceFunction(f) :> IAnimation<'Model, 'Value>


module Animation =

    /// Creates an animation from the given space and distance-time functions.
    let create (spaceFunction : ISpaceFunction<'Value>) (distanceTimeFunction : IDistanceTimeFunction) =
        { NextState = State.Stopped
          CurrentState = State.Stopped
          CurrentValue = spaceFunction.Invoke 0.0
          CurrentPosition = 0.0
          SpaceFunction = spaceFunction
          DistanceTimeFunction = distanceTimeFunction
          Observers = HashSet.empty } :> IAnimation<'Model, 'Value>

    /// Empty animation
    let empty<'Model, 'Value> : IAnimation<'Model, 'Value> =
        create SpaceFunction<'Value>.Default DistanceTimeFunction.Default

[<AutoOpen>]
module IAnimationStateQueryExtensions =
    type IAnimation<'Model> with

        /// Returns whether the animation is running.
        member x.IsRunning = x.State |> function State.Running _ -> true | _ -> false

        /// Returns whether the animation is stopped.
        member x.IsStopped = x.State = State.Stopped

        /// Returns whether the animation is finished.
        member x.IsFinished = x.State = State.Finished

        /// Returns whether the animation is paused.
        member x.IsPaused = x.State |> function State.Paused _ -> true | _ -> false