namespace Aardvark.UI.Anewmation

open Aardvark.Base
open OptimizedClosures

[<AbstractClass>]
type private AbstractAnimationInstance<'Model, 'Value, 'Definition when 'Definition :> IAnimation<'Model, 'Value>>(name : Symbol, definition : 'Definition) =
    let mutable stateMachine = StateMachine.initial<'Value>

    member x.Name = name
    member x.State = stateMachine.Holder.State
    member x.Value = stateMachine.Holder.Value
    member x.Definition = definition
    member x.StateMachine = &stateMachine

    abstract member Perform : Action -> unit
    abstract member Commit: 'Model -> 'Model

    interface IAnimation with
        member x.Duration = definition.Duration
        member x.TotalDuration = definition.TotalDuration
        member x.DistanceTime(localTime) = definition.DistanceTime(localTime)

    interface IAnimationInstance<'Model> with
        member x.Name = x.Name
        member x.State = x.State
        member x.Perform(action) = x.Perform(action)
        member x.Commit(model) = x.Commit(model)
        member x.Definition = x.Definition :> IAnimation<'Model>

    interface IAnimationInstance<'Model, 'Value> with
        member x.Value = x.Value
        member x.Definition = x.Definition :> IAnimation<'Model, 'Value>


type private AnimationInstance<'Model, 'Value>(name : Symbol, definition : Animation<'Model, 'Value>) =
    inherit AbstractAnimationInstance<'Model, 'Value, Animation<'Model, 'Value>>(name, definition)

    override x.Perform(action) =
        StateMachine.enqueue action &x.StateMachine

    override x.Commit(model) =
        let definition = x.Definition
        let evaluate t = definition.Evaluate(model, t)
        let events = StateMachine.run evaluate &x.StateMachine

        let mutable result = model
        Observable.notify x.Definition.Observable x.Name events &result
        result

/// An animation consists of a space function and a distance-time function.
/// The space function defines the animation values based on position (parameterized by arc length in the range of [0, 1]).
/// The distance-time function controls how the position changes over time.
and private Animation<'Model, 'Value> =
    {
        SpaceFunction : FSharpFunc<'Model, float, 'Value>
        DistanceTimeFunction : DistanceTimeFunction
        Duration : Duration
        Observable : Observable<'Model, 'Value>
    }

    member x.Create(name) =
        AnimationInstance(name, x)

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.DistanceTime(localTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(localTime / x.Duration)

    member x.Evaluate(model : 'Model, localTime : LocalTime) : 'Value =
        x.SpaceFunction.Invoke(model, x.DistanceTime(localTime))

    member x.Scale(duration) =
        { x with Duration = duration }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    member x.Subscribe(event : EventType, callback : Symbol -> 'Value -> 'Model -> 'Model) =
        { x with Observable = x.Observable |> Observable.subscribe event callback }

    member x.UnsubscribeAll() =
        { x with Observable = Observable.empty }

    interface IAnimation with
        member x.Duration = x.Duration
        member x.TotalDuration = x.TotalDuration
        member x.DistanceTime(localTime) = x.DistanceTime(localTime)

    interface IAnimation<'Model> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(event, callback) = x.Subscribe(event, callback) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


module Animation =

    [<AbstractClass>]
    type private SpaceFunction<'Model, 'Value>() =
        static let defaultFunction = FSharpFunc<'Model, float, 'Value>.Adapt (fun _ _ -> Unchecked.defaultof<'Value>)
        static member Default = defaultFunction

    /// Empty animation.
    let empty<'Model, 'Value> : IAnimation<'Model, 'Value> =
        { SpaceFunction = SpaceFunction.Default
          DistanceTimeFunction = DistanceTimeFunction.empty
          Duration = Duration.zero
          Observable = Observable.empty } :> IAnimation<'Model, 'Value>

    /// Creates an animation from the given space function.
    let create' (spaceFunction : 'Model -> float -> 'Value) =
        { SpaceFunction = FSharpFunc<_,_,_>.Adapt spaceFunction
          DistanceTimeFunction = DistanceTimeFunction.empty
          Duration = Duration.zero
          Observable = Observable.empty } :> IAnimation<'Model, 'Value>

    /// Creates an animation from the given space function.
    let create (spaceFunction : float -> 'Value) : IAnimation<'Model, 'Value> =
        create' (fun _ s -> spaceFunction s)