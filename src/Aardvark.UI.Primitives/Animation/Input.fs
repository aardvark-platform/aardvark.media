namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

type private InputMapping<'Model, 'T, 'Input, 'U> =
    {
        StateMachine : StateMachine<'U>
        Animation : IAnimation<'Model, 'T>
        Input : IAnimation<'Model, 'Input>
        Mapping : System.Func<'Model, 'T, 'Input, 'U>
        Observable : Observable<'Model, 'U>
    }

    member x.Perform(action) =
        { x with
            StateMachine = x.StateMachine |> StateMachine.enqueue action
            Animation = x.Animation.Perform(action)
            Input = x.Input.Perform(action) }

    member x.Scale(duration) =
        { x with Animation = x.Animation.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with Animation = x.Animation.Ease(easing, compose) }

    member x.Loop(iterations, mode) =
        { x with Animation = x.Animation.Loop(iterations, mode) }

    member x.Subscribe(observer : IAnimationObserver<'Model, 'U>) =
        { x with Observable = x.Observable |> Observable.subscribe observer }

    member x.UnsubscribeAll() =
        { x with
            Animation = x.Animation.UnsubscribeAll()
            Observable = Observable.empty }

    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with
            Animation = x.Animation.Unsubscribe(observer)
            Observable = x.Observable |> Observable.unsubscribe observer }

    member x.Commit(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        // Commit wrapped animation and input
        let animation, input, model =
            let model = x.Animation.Commit(lens, name, model)
            let animation = model |> Optic.get lens |> unbox<IAnimation<'Model, 'T>>

            let model = x.Input.Commit(lens, name, model)
            let input = model |> Optic.get lens |> unbox<IAnimation<'Model, 'Input>>

            animation, input, model

        // Process all actions, from oldest to newest
        let machine, events =
            let eval = fun _ -> x.Mapping.Invoke(model, animation.Value, input.Value)
            x.StateMachine |> StateMachine.run eval

        // Notify observers about changes
        let model =
            model |> Optic.set lens (
                { x with
                    StateMachine = machine;
                    Animation = animation
                    Input = input } :> IAnimation<'Model>
            )

        x.Observable |> Observable.notify name events model

    interface IAnimation<'Model> with
        member x.State = x.StateMachine.Holder.State
        member x.Duration = x.Animation.Duration
        member x.TotalDuration = x.Animation.TotalDuration
        member x.DistanceTime(localTime) = x.Animation.DistanceTime(localTime)
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Commit(lens, name, model) = x.Commit(lens, name, model)
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'U> with
        member x.Value = x.StateMachine.Holder.Value
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'U>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'U>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'U>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'U>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'U>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'U>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'U>


[<AutoOpen>]
module AnimationInputExtensions =

    module Animation =

        /// Applies the given input animation using the given mapping function.
        /// Note that in contrast to Animation.map2, the input animation is not affected by calls to Scale(), Ease(), and Loop() to
        /// the resulting animation. Likewise, the state and events of the resulting animation remain independent of the input
        /// animation.
        let input' (mapping : 'Model -> 'Input -> 'T -> 'U) (input : IAnimation<'Model, 'Input>) (animation : IAnimation<'Model, 'T>) =
            { StateMachine = StateMachine.initial
              Animation = animation
              Input = input.UnsubscribeAll()
              Mapping = System.Func<_,_,_,_> (fun model value input -> value |> mapping model input)
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Applies the given input animation using the given mapping function.
        /// Note that in contrast to Animation.map2, the input animation is not affected by calls to Scale(), Ease(), and Loop() to
        /// the resulting animation. Likewise, the state and events of the resulting animation remain independent of the input
        /// animation.
        let input (mapping : 'Input -> 'T -> 'U) (input : IAnimation<'Model, 'Input>) (animation : IAnimation<'Model, 'T>) =
            (input, animation) ||> input' (fun _ input value -> mapping input value)