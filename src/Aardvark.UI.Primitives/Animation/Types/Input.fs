namespace Aardvark.UI.Animation

open Aardvark.Base
open OptimizedClosures

type internal InputMappingInstance<'Model, 'T, 'Input, 'U>(name : Symbol, definition : InputMapping<'Model, 'T, 'Input, 'U>) =
    inherit AbstractAnimationInstance<'Model, 'U, InputMapping<'Model, 'T, 'Input, 'U>>(name, definition)

    let wrapped = definition.Animation.Create(name)
    let input = definition.Input.Create(name)

    override x.Perform(action) =
        wrapped.Perform(action)
        input.Perform(action)
        StateMachine.enqueue action x.StateMachine

    override x.Commit(model, tick) =
        // Commit members
        let mutable result = input.Commit(wrapped.Commit(model, tick), tick)

        //// Process all actions, from oldest to newest
        let evaluate _ = definition.Mapping.Invoke(result, wrapped.Value, input.Value)
        StateMachine.run evaluate tick x.EventQueue x.StateMachine

        // Notify observers about changes
        Observable.notify x.Definition.Observable x.Name x.EventQueue &result

        result


and internal InputMapping<'Model, 'T, 'Input, 'U> =
    {
        Animation : IAnimation<'Model, 'T>
        Input : IAnimation<'Model, 'Input>
        Mapping : FSharpFunc<'Model, 'T, 'Input, 'U>
        Observable : Observable<'Model, 'U>
    }

    member x.Create(name) =
        InputMappingInstance(name, x)

    member x.Scale(duration) =
        { x with Animation = x.Animation.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with Animation = x.Animation.Ease(easing, compose) }

    member x.Loop(iterations, mode) =
        { x with Animation = x.Animation.Loop(iterations, mode) }

    member x.Subscribe(event : EventType, callback : Symbol -> 'U -> 'Model -> 'Model) =
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

    interface IAnimation<'Model, 'U> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model, 'U>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'U>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'U>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'U>
        member x.Subscribe(event, callback) = x.Subscribe(event, callback) :> IAnimation<'Model, 'U>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'U>


[<AutoOpen>]
module AnimationInputExtensions =

    module Animation =

        /// Applies the given input animation using the given mapping function.
        /// Note that in contrast to Animation.map2, the input animation is not affected by calls to Scale(), Ease(), and Loop() to
        /// the resulting animation. Likewise, the state and events of the resulting animation remain independent of the input
        /// animation.
        let input' (mapping : 'Model -> 'Input -> 'T -> 'U) (input : IAnimation<'Model, 'Input>) (animation : IAnimation<'Model, 'T>) =
            { Animation = animation
              Input = input.UnsubscribeAll()
              Mapping = FSharpFunc<_,_,_,_>.Adapt (fun model value input -> value |> mapping model input)
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Applies the given input animation using the given mapping function.
        /// Note that in contrast to Animation.map2, the input animation is not affected by calls to Scale(), Ease(), and Loop() to
        /// the resulting animation. Likewise, the state and events of the resulting animation remain independent of the input
        /// animation.
        let input (mapping : 'Input -> 'T -> 'U) (input : IAnimation<'Model, 'Input>) (animation : IAnimation<'Model, 'T>) =
            (input, animation) ||> input' (fun _ -> mapping)