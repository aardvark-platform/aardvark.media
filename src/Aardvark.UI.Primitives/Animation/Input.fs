namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open Aether

type private InputMapping<'Model, 'T, 'Input, 'U> =
    {
        /// The current state of the primary input.
        State : State

        /// Wrapped animation.
        Animation : IAnimation<'Model, 'T>

        /// Input animation.
        Input : IAnimation<'Model, 'Input>

        /// Mapping from input to output.
        Mapping : Func<'T, 'Input, 'U>

        /// Observers to be notified of changes.
        Observers : HashMap<IAnimationObserver<'Model>, IAnimationObserver<'Model, 'U>>
    }

    member x.Duration =
        x.Animation.Duration

    /// Stops the animation and resets it.
    member x.Stop() =
        { x with
            Animation = x.Animation.Stop()
            Input = x.Input.Stop() }

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    member x.Start(globalTime) =
        { x with
            Animation = x.Animation.Start(globalTime)
            Input = x.Input.Start(globalTime) }

    /// Pauses the animation if it is running or has started.
    member x.Pause(globalTime) =
        { x with
            Animation = x.Animation.Pause(globalTime)
            Input = x.Input.Pause(globalTime) }

    /// Resumes the animation from the point it was paused.
    /// Has no effect if the animation is not paused.
    member x.Resume(globalTime) =
        { x with
            Animation = x.Animation.Resume(globalTime)
            Input = x.Input.Resume(globalTime) }

    /// Updates the animation to the given global time.
    member x.Update(globalTime) =
        { x with
            Animation = x.Animation.Update(globalTime)
            Input = x.Input.Update(globalTime) }

    member x.Scale(duration) =
        { x with Animation = x.Animation.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with Animation = x.Animation.Ease(easing, compose) }

    member x.Loop(iterations, mode) =
        { x with Animation = x.Animation.Loop(iterations, mode) }

    /// Registers a new observer.
    member x.Subscribe(observer : IAnimationObserver<'Model, 'U>) =
        if not observer.IsEmpty then
            let key = observer :> IAnimationObserver<'Model>
            { x with Observers = x.Observers |> HashMap.add key observer }
        else
            x

    /// Removes all observers.
    member x.UnsubscribeAll() =
        { x with
            Animation = x.Animation.UnsubscribeAll()
            Input = x.Input.UnsubscribeAll()
            Observers = HashMap.empty }

    /// Removes the given observer (if present).
    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with
            Animation = x.Animation.Unsubscribe(observer)
            Input = x.Input.Unsubscribe(observer)
            Observers = x.Observers |> HashMap.remove observer }

    /// Notifies all observers.
    member x.Notify(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        let animation, input, model =
            let model = x.Animation.Notify(lens, name, model)
            let animation = model |> Optic.get lens |> unbox<IAnimation<'Model, 'T>>

            let model = x.Input.Notify(lens, name, model)
            let input = model |> Optic.get lens |> unbox<IAnimation<'Model, 'Input>>

            animation, input, model

        let next = { x with Animation = animation; Input = input; State = animation.State }
        let model = model |> Optic.set lens (next :> IAnimation<'Model>)

        let events =
            (x.State, next.State) ||> Events.compute |> List.sort

        let notify value model event =
            (model, x.Observers |> HashMap.toSeq)
            ||> Seq.fold (fun model (_, obs) -> obs.OnNext(model, name, event, value))

        if List.isEmpty events then
            model
        else
            let value = x.Mapping.Invoke(animation.Value, input.Value)
            (model, events) ||> Seq.fold (notify value)

    interface IAnimation<'Model> with
        member x.State = x.State
        member x.Duration = x.Duration
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

    interface IAnimation<'Model, 'U> with
        member x.Value = (x.Animation.Value, x.Input.Value) |> x.Mapping.Invoke
        member x.Stop() = x.Stop() :> IAnimation<'Model, 'U>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model, 'U>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model, 'U>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model, 'U>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model, 'U>
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
        let input (mapping : 'Input -> 'T -> 'U) (input : IAnimation<'Model, 'Input>) (animation : IAnimation<'Model, 'T>) =
            { State = animation.State
              Animation = animation
              Input = input
              Mapping = Func<_,_,_> (fun value input -> value |> mapping input)
              Observers = HashMap.empty } :> IAnimation<'Model, 'U>