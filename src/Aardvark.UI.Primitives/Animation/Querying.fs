namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive

[<AutoOpen>]
module AnimatorQuerying =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Animator =

        open Aether
        open InternalAnimatorUtilities
        open InternalAnimatorUtilities.Converters

        open System.Collections.Generic

        /// Tries to get the (untyped) animation instance with the given name.
        /// The name can be a string or Symbol. Returns None if the
        /// animation does not exist.
        let inline tryGetUntyped (name : ^Name) (model : 'Model) =
            let sym = name |> symbol Unchecked.defaultof<NameConverter>
            let animator = model |> Optic.get Lenses.get<'Model>
            animator.Animations |> HashMap.tryFind sym

        /// <summary>
        /// Gets the (untyped) animation instance with the given name.
        /// The name can be a string or Symbol.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the animation does not exist.</exception>
        let inline getUntyped (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.defaultWith (raise <| KeyNotFoundException())

        /// Tries to get the animation instance with the given name.
        /// The name can be a string or Symbol. Returns None if
        /// the animation does not exist or is not of the expected type.
        let inline tryGet (name : ^Name) (model : 'Model) : IAnimation<'Model, 'Value> option =
            model |> tryGetUntyped name |> Option.bind (function
                | :? IAnimation<'Model, 'Value> as a -> Some a
                | _ -> None
            )

        /// <summary>
        /// Gets the animation instance with the given name.
        /// The name can be a string or Symbol.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the animation instance does not exist.</exception>
        /// <exception cref="InvalidCastException">Thrown if the animation instance is not of the expected type.</exception>
        let inline get (name : ^Name) (model : 'Model) : IAnimation<'Model, 'Value> =
            model |> getUntyped name |> unbox

        /// Returns whether an animation instance with the given name exists.
        /// The name can be a string or Symbol.
        let inline exists (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.isSome

        /// Tries to get the state of the animation instance with the given name.
        /// The name can be a string or Symbol. Returns None if the
        /// animation instance does not exist.
        let inline tryGetState (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.map (fun a -> a.State)

        /// <summary>
        /// Gets the state of the animation instance with the given name.
        /// The name can be a string or Symbol.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the animation instance does not exist.</exception>
        let inline getState (name : ^Name) (model : 'Model) =
            model |> getUntyped name |> fun a -> a.State

        /// Returns whether the animation instance with the given name is running.
        /// The name can be a string or Symbol. Returns false if the
        /// animation instance does not exist.
        let inline isRunning (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.map (fun a -> a.IsRunning) |> Option.defaultValue false

        /// Returns whether the animation instance with the given name is stopped.
        /// The name can be a string or Symbol. Returns true if the
        /// animation instance does not exist.
        let inline isStopped (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.map (fun a -> a.IsStopped) |> Option.defaultValue true

        /// Returns whether the animation instance with the given name is finished.
        /// The name can be a string or Symbol. Returns true if the
        /// animation instance does not exist.
        let inline isFinished (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.map (fun a -> a.IsFinished) |> Option.defaultValue true

        /// Returns whether the animation instance with the given name is paused.
        /// The name can be a string or Symbol. Returns false if the
        /// animation instance does not exist.
        let inline isPaused (name : ^Name) (model : 'Model) =
            model |> tryGetUntyped name |> Option.map (fun a -> a.IsPaused) |> Option.defaultValue false