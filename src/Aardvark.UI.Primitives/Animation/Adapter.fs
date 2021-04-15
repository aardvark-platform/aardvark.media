namespace Aardvark.UI.Anewmation

open Aardvark.Base

type private AdapterInstance<'Model, 'Value>(name : Symbol, definition : Adapter<'Model, 'Value>) =
    inherit AbstractAnimationInstance<'Model, 'Value, Adapter<'Model, 'Value>>(name, definition)

    let wrapped = definition.Animation.Create(name)

    override x.Perform(action) =
        wrapped.Perform(action)
        StateMachine.enqueue action &x.StateMachine

    override x.Commit(model) =

        // Commit wrapped animation
        let mutable result = wrapped.Commit(model)

        // Process all actions, from oldest to newest
        let evaluate _ = Unchecked.defaultof<'Value>
        StateMachine.run evaluate &x.EventQueue &x.StateMachine

        // Notify observers about changes
        Observable.notify x.Definition.Observable x.Name &x.EventQueue &result

        result


and private Adapter<'Model, 'Value> =
    {
        Animation : IAnimation<'Model>
        Observable : Observable<'Model, 'Value>
    }

    member x.Create(name) =
        AdapterInstance(name, x)

    member x.Scale(duration) =
        { x with Animation = x.Animation.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with Animation = x.Animation.Ease(easing, compose) }

    member x.Loop(iterations, mode) =
        { x with Animation = x.Animation.Loop(iterations, mode) }

    member x.Subscribe(event : EventType, callback : Symbol -> 'Value -> 'Model -> 'Model) =
        { x with Observable = x.Observable |> Observable.subscribe event callback }

    member x.UnsubscribeAll() =
        { x with
            Animation = x.Animation.UnsubscribeAll()
            Observable = Observable.empty }

    interface IAnimation with
        member x.Duration = x.Animation.Duration
        member x.TotalDuration = x.Animation.TotalDuration
        member x.DistanceTime(localTime) = x.Animation.DistanceTime(localTime)

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

[<AutoOpen>]
module AnimationAdapterExtensions =

    module Animation =

        /// Creates a typed animation which always returns a default value.
        let adapter (animation : IAnimation<'Model>) : IAnimation<'Model, 'Value> =
            { Animation = animation
              Observable = Observable.empty } :> IAnimation<'Model, 'Value>