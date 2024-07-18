namespace Aardvark.UI.Animation

open Aardvark.Base
open Aether

[<AutoOpen>]
module AnimationCallbackExtensions =

    module Animation =

        /// Removes all callbacks from the animation.
        let unsubscribeAll (animation : IAnimation<'Mode, 'Value>) =
            animation.UnsubscribeAll()

        /// Registers a callback that is invoked for events of the given type.
        let onEvent (event : EventType) (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation.Subscribe(event, callback)

        /// Registers a callback that is invoked after the animation is stopped manually.
        let onStop (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Stop callback

        /// Registers a callback that is invoked after the animation is started or restarted.
        let onStart (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Start callback

        /// Registers a callback that is invoked after the animation is paused.
        let onPause (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Pause callback

        /// Registers a callback that is invoked after the animation is resumed.
        let onResume (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Resume callback

        /// Registers a callback that is invoked after each animation tick (including the last tick, as well as start and restart events).
        let onProgress (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Progress callback

        /// Registers a callback that is invoked after the animation has finished.
        let onFinalize (callback : Symbol -> 'Value -> 'Model -> 'Model) (animation : IAnimation<'Model, 'Value>) =
            animation |> onEvent EventType.Finalize callback

        /// Links the animation to a field in the model by registering
        /// a callback that uses the given lens to modify its value as the animation progresses.
        let link (lens : Lens<'Model, 'Value>) (animation : IAnimation<'Model, 'Value>) =
            animation |> onProgress (fun _ -> Optic.set lens)

        /// Links the animation to a field in the model by registering
        /// a callback that uses the given lens and mapping to modify its value as the animation progresses.
        let linkMap (lens : Lens<'Model, 'Output>) (mapping : 'Value -> 'Output) (animation : IAnimation<'Model, 'Value>) =
            animation |> onProgress (fun _ value model -> (mapping value, model) ||> Optic.set lens)