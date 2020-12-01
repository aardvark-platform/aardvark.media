namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open Aether

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

        /// Curve defining the animation values based on
        /// its position (parameterized by arc length in the range of [0, 1]).
        SpaceFunction : Func<float, 'Value>

        /// Function controlling how the animation position changes over time.
        /// Returns a flag indicating if the animation has finished and the position.
        DistanceTimeFunction : DistanceTimeFunction

        /// Observers to be notified of changes.
        Observers : HashMap<IAnimationObserver<'Model>, IAnimationObserver<'Model, 'Value>>
    }

    /// Stops the animation and resets it.
    member x.Stop() =
        { x with
            NextState = State.Stopped
            CurrentValue = x.SpaceFunction.Invoke 0.0 }

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    member x.Start(globalTime : MicroTime) =
        { x with
            NextState = State.Running globalTime
            CurrentValue = x.SpaceFunction.Invoke 0.0 }

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
            let f, s = (globalTime - startTime) |> x.DistanceTimeFunction.Invoke
            let v = x.SpaceFunction.Invoke s

            { x with
                NextState = if f then State.Finished else State.Running startTime
                CurrentValue = v }
        | _ ->
            x

    member x.Scale(duration) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Scale(duration)}

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    /// Registers a new observer.
    member x.Subscribe(observer : IAnimationObserver<'Model, 'Value>) =
        if not observer.IsEmpty then
            let key = observer :> IAnimationObserver<'Model>
            { x with Observers = x.Observers |> HashMap.add key observer }
        else
            x

    /// Removes all observers.
    member x.UnsubscribeAll() =
        { x with Observers = HashMap.empty }

    /// Removes the given observer (if present).
    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with Observers = x.Observers |> HashMap.remove observer }

    /// Notifies all observers.
    member x.Notify(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        let events = (x.CurrentState, x.NextState) ||> Events.compute |> List.sort
        let model = model |> Optic.set lens ({ x with CurrentState = x.NextState } :> IAnimation<'Model>)

        let notify model event =
            (model, x.Observers |> HashMap.toSeq)
            ||> Seq.fold (fun model (_, obs) -> obs.OnNext(model, name, event, x.CurrentValue))

        (model, events) ||> Seq.fold notify

    interface IAnimation<'Model> with
        member x.State = x.CurrentState
        member x.Duration = x.DistanceTimeFunction.Duration
        member x.Stop() = x.Stop() :> IAnimation<'Model>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Notify(lens, name, model) = x.Notify(lens, name, model)
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.CurrentValue
        member x.Stop() = x.Stop() :> IAnimation<'Model, 'Value>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model, 'Value>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model, 'Value>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model, 'Value>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'Value>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


module Animation =

    [<AbstractClass>]
    type private SpaceFunction<'Value>() =
        static let defaultFunction = Func<float, 'Value>(fun _ -> Unchecked.defaultof<'Value>)
        static member Default = defaultFunction

    /// Empty animation.
    let empty<'Model, 'Value> : IAnimation<'Model, 'Value> =
        { NextState = State.Stopped
          CurrentState = State.Stopped
          CurrentValue = Unchecked.defaultof<'Value>
          SpaceFunction = SpaceFunction.Default
          DistanceTimeFunction = DistanceTimeFunction.empty
          Observers = HashMap.empty } :> IAnimation<'Model, 'Value>

    /// Creates an animation from the given space function.
    let create (spaceFunction : float -> 'Value) =
        { NextState = State.Stopped
          CurrentState = State.Stopped
          CurrentValue = spaceFunction 0.0
          SpaceFunction = Func<_,_> spaceFunction
          DistanceTimeFunction = DistanceTimeFunction.empty
          Observers = HashMap.empty } :> IAnimation<'Model, 'Value>


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