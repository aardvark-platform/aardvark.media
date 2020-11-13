namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open Aether

/// Observer for invoking callbacks on animation changes.
type private Observer<'Model, 'Value> =
    {
        Callbacks : HashMap<EventType, Func<Symbol, 'Value, 'Model, 'Model>>
    }

    /// Returns whether the observer does not have any callbacks.
    member x.IsEmpty =
        x.Callbacks |> HashMap.isEmpty

    /// Adds a callback for the given event.
    member x.Add(callback, event) =
        let cb =
            match x.Callbacks |> HashMap.tryFind event with
            | Some l -> fun name value model -> l.Invoke(name, value, model) |> callback name value
            | None -> callback

        { x with Callbacks = x.Callbacks |> HashMap.add event (Func<_,_,_,_>(cb)) }

    /// Invoked on animation events.
    member x.OnNext(model, name, event, value) =
        HashMap.tryFind event x.Callbacks
        |> Option.map (fun cb -> cb.Invoke(name, value, model))
        |> Option.defaultValue model

    interface IAnimationObserver<'Model, 'Value> with
        member x.IsEmpty = x.IsEmpty
        member x.Add(callback, event) = x.Add(callback, event) :> IAnimationObserver<'Model, 'Value>
        member x.OnNext(model, name, event, value) = x.OnNext(model, name, event, value)


module Observer =

    /// Creates a new observer with the given callbacks.
    let create (callbacks : Map<EventType, Symbol -> 'Value -> 'Model -> 'Model>) =
        let map = callbacks |> Map.map (fun _ f -> Func<_,_,_,_> f)
        { Callbacks = HashMap.ofMap map } :> IAnimationObserver<'Model, 'Value>

    /// Empty observer without callbacks.
    let empty<'Model,'Value> : IAnimationObserver<'Model, 'Value> =
        (create Map.empty) :> IAnimationObserver<'Model, 'Value>

    /// Returns whether the observer is empty.
    let isEmpty (observer : IAnimationObserver<'Model, 'Value>) =
        observer.IsEmpty


    /// Adds a callback for the given event type to the observer.
    let onEvent (event : EventType) (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer.Add((fun _ value model -> callback value; model), event)

    /// Adds a callback for the given event type to the observer.
    let onEvent' (event : EventType) (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer.Add(callback, event)


    /// Adds a callback to the observer that is invoked after the animation is stopped manually.
    let onStop (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Stop callback

    /// Adds a callback to the observer that is invoked after the animation is stopped manually.
    let onStop' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Stop callback


    /// Adds a callback to the observer that is invoked after the animation is started or restarted.
    let onStart (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Start callback

    /// Adds a callback to the observer that is invoked after the animation is started or restarted.
    let onStart' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Start callback


    /// Adds a callback to the observer that is invoked after the animation is paused.
    let onPause (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Pause callback

    /// Adds a callback to the observer that is invoked after the animation is paused.
    let onPause' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Pause callback


    /// Adds a callback to the observer that is invoked after the animation is resumed.
    let onResume (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Resume callback

    /// Adds a callback to the observer that is invoked after the animation is resumed.
    let onResume' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Resume callback


    /// Adds a callback to the observer that is invoked after each animation tick (including the last tick, as well as start and restart events).
    let onProgress (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Progress callback

    /// Adds a callback to the observer that is invoked after each animation tick (including the last tick, as well as start and restart events).
    let onProgress' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Progress callback


    /// Adds a callback to the observer that is invoked after the animation has finished.
    let onFinalize (callback : ('Value -> unit)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent EventType.Finalize callback

    /// Adds a callback to the observer that is invoked after the animation has finished.
    let onFinalize' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onEvent' EventType.Finalize callback


    /// Links the animation to a field in the model by registering
    /// a callback that uses the given lens to modify its value as the animation progresses.
    let link (lens : Lens<'Model, 'Value>) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onProgress' (fun _ -> Optic.set lens)

    /// Links the animation to a field in the model by registering
    /// a callback that uses the given lens and mapping to modify its value as the animation progresses.
    let linkMap (lens : Lens<'Model, 'Output>) (mapping : 'Value -> 'Output) (observer : IAnimationObserver<'Model, 'Value>) =
        observer |> onProgress' (fun _ value model -> (mapping value, model) ||> Optic.set lens)


[<AutoOpen>]
module AnimationObserverExtensions =

    module Animation =

        /// Registers the given observer for the animation.
        let subscribe (observer : IAnimationObserver<'Model, 'Value>) (animation : IAnimation<'Model, 'Value>) =
            animation.Subscribe(observer)

        /// Removes the given observer from the animation (if present).
        let unsubscribe (observer : IAnimationObserver<'Model>) (animation : IAnimation<'Model, 'Value>) =
            animation.Unsubscribe(observer)

        /// Removes all observers from the animation.
        let unsubscribeAll (animation : IAnimation<'Mode, 'Value>) =
            animation.UnsubscribeAll()


        /// Creates and registers an observer with a callback that is invoked for events of the given type.
        let onEvent (event : EventType) (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onEvent event callback)

        /// Creates and registers an observer with a callback that is invoked for events of the given type.
        let onEvent' (event : EventType) (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onEvent' event callback)


        /// Creates and registers an observer with a callback that is invoked after the animation is stopped manually.
        let onStop (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onStop callback)

        /// Creates and registers an observer with a callback that is invoked after the animation is stopped manually.
        let onStop' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onStop' callback)


        /// Creates and registers an observer with a callback that is invoked after the animation is started or restarted.
        let onStart (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onStart callback)

        /// Creates and registers an observer with a callback that is invoked after the animation is started or restarted.
        let onStart' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onStart' callback)


        /// Creates and registers an observer with a callback that is invoked after the animation is paused.
        let onPause (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onPause callback)

        /// Creates and registers an observer with a callback that is invoked after the animation is paused.
        let onPause' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onPause' callback)


        /// Creates and registers an observer with a callback that is invoked after the animation is resumed.
        let onResume (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onResume callback)

        /// Creates and registers an observer with a callback that is invoked after the animation is resumed.
        let onResume' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onResume' callback)


        /// Creates and registers an observer with a callback that is invoked after each animation tick (including the last tick, as well as start and restart events).
        let onProgress (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onProgress callback)

        /// Creates and registers an observer with a callback that is invoked after each animation tick (including the last tick, as well as start and restart events).
        let onProgress' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onProgress' callback)


        /// Creates and registers an observer with a callback that is invoked after the animation has finished.
        let onFinalize (callback : ('Value -> unit)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onFinalize callback)

        /// Creates and registers an observer with a callback that is invoked after the animation has finished.
        let onFinalize' (callback : (Symbol -> 'Value -> 'Model -> 'Model)) (animation : IAnimation<'Model, 'Value>) =
            animation |> subscribe (Observer.empty |> Observer.onFinalize' callback)


        /// Links the animation to a field in the model by registering
        /// a callback that uses the given lens to modify its value as the animation progresses.
        let link (lens : Lens<'Model, 'Value>) (animation : IAnimation<'Model, 'Value>) =
            animation |> onProgress' (fun _ -> Optic.set lens)

        /// Links the animation to a field in the model by registering
        /// a callback that uses the given lens and mapping to modify its value as the animation progresses.
        let linkMap (lens : Lens<'Model, 'Output>) (mapping : 'Value -> 'Output) (animation : IAnimation<'Model, 'Value>) =
            animation |> onProgress' (fun _ value model -> (mapping value, model) ||> Optic.set lens)