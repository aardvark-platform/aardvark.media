namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

/// An animation consists of a space function and a distance-time function.
/// The space function defines the animation values based on position (parameterized by arc length in the range of [0, 1]).
/// The distance-time function controls how the position changes over time.
type private Animation<'Model, 'Value> =
    {
        StateMachine : StateMachine<'Value>
        SpaceFunction : System.Func<'Model, float, 'Value>
        DistanceTimeFunction : DistanceTimeFunction
        Duration : Duration
        Observable : Observable<'Model, 'Value>
    }

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.DistanceTime(localTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(localTime / x.Duration)

    member x.Evaluate(model : 'Model, localTime : LocalTime) =
        x.DistanceTime(localTime) |> Param.map (fun s -> x.SpaceFunction.Invoke(model, s))

    member x.Perform(action : Action) =
        { x with StateMachine = x.StateMachine |> StateMachine.enqueue action }

    member x.Scale(duration) =
        { x with Duration = duration }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    member x.Subscribe(observer : IAnimationObserver<'Model, 'Value>) =
        { x with Observable = x.Observable |> Observable.subscribe observer }

    member x.UnsubscribeAll() =
        { x with Observable = Observable.empty }

    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with Observable = x.Observable |> Observable.unsubscribe observer }

    member x.Commit(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        // Process all actions, from oldest to newest
        let machine, events =
            x.StateMachine |> StateMachine.run (fun t -> x.Evaluate(model, t))

        // Notify observers about changes
        let model =
            model |> Optic.set lens ({ x with StateMachine = machine } :> IAnimation<'Model>)

        x.Observable |> Observable.notify name events model

    interface IAnimation<'Model> with
        member x.State = x.StateMachine.Holder.State
        member x.Duration = x.Duration
        member x.TotalDuration = x.TotalDuration
        member x.DistanceTime(localTime) = x.DistanceTime(localTime)
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Commit(lens, name, model) = x.Commit(lens, name, model)
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.StateMachine.Holder.Value
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'Value>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


module Animation =

    [<AbstractClass>]
    type private SpaceFunction<'Model, 'Value>() =
        static let defaultFunction = System.Func<'Model, float, 'Value>(fun _ _ -> Unchecked.defaultof<'Value>)
        static member Default = defaultFunction

    /// Empty animation.
    let empty<'Model, 'Value> : IAnimation<'Model, 'Value> =
        { StateMachine = StateMachine.initial
          SpaceFunction = SpaceFunction.Default
          DistanceTimeFunction = DistanceTimeFunction.empty
          Duration = Duration.zero
          Observable = Observable.empty } :> IAnimation<'Model, 'Value>

    /// Creates an animation from the given space function.
    let create' (spaceFunction : 'Model -> float -> 'Value) =
        { StateMachine = StateMachine.initial
          SpaceFunction = System.Func<_,_,_> spaceFunction
          DistanceTimeFunction = DistanceTimeFunction.empty
          Duration = Duration.zero
          Observable = Observable.empty } :> IAnimation<'Model, 'Value>

    /// Creates an animation from the given space function.
    let create (spaceFunction : float -> 'Value) : IAnimation<'Model, 'Value> =
        create' (fun _ s -> spaceFunction s)