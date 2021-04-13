﻿namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive

// Need to be public for inlined functions with SRTPs
module InternalAnimatorUtilities =

    // These utilities make it possible to pass a string or Symbol as identifier.
    module Converters =
        type NameConverter =
            static member inline GetSymbol(name : string)   = Sym.ofString name
            static member inline GetSymbol(symbol : Symbol) = symbol

        [<AutoOpen>]
        module internal Aux =
            let inline symbol (_ : ^z) (name : ^Name) =
                ((^z or ^Name) : (static member GetSymbol : ^Name -> Symbol) (name))

    // We cache the lenses for each model type so the user doesn't have to keep passing them around.
    module Lenses =
        open Aether
        open System
        open System.Collections.Generic
        open System.Collections.Concurrent

        let private cache = ConcurrentDictionary<Type, obj>()

        let get<'Model> : Lens<'Model, Animator<'Model>>=
            let t = typeof<'Model>
            match cache.TryGetValue(t) with
            | (true, lens) -> lens |> unbox
            | _ ->
                let message =
                    let keys = cache.Keys |> Seq.toArray |> Array.map (string >> (+) "      ")

                    if keys.Length > 0 then
                        let n = Environment.NewLine
                        let lines = n + (keys |> String.concat n)
                        sprintf "[Animation] Lens for model type '%A' not found. There are %d lenses for the following types:%s" t keys.Length lines
                    else
                        "[Animation] No lenses registered. Did you call Animator.initial?"

                Log.error "%s" message
                raise <| KeyNotFoundException()

        let set (lens : Lens<'Model, Animator<'Model>>) =
            let t = typeof<'Model>
            let lens = box lens
            cache.AddOrUpdate(t, lens, fun _ _ -> lens) |> ignore

    // Utility to generate names.
    module Name =
        let mutable private next = 0
        let get() =
            let id, _ = next, inc &next
            Sym.ofString <| sprintf "_animation%d" id


module Animator =
    open Aether
    open Aether.Operators
    open InternalAnimatorUtilities
    open InternalAnimatorUtilities.Converters
    open Aardvark.UI.Anewmation

    // Global time for animations
    module private Time =
        open System.Diagnostics

        let private sw = Stopwatch.StartNew()

        let get() =
            sw.Elapsed.MicroTime |> GlobalTime.Timestamp

    [<AutoOpen>]
    module internal Querying =

        // Tries to get the (untyped) animation with the given name.
        let inline tryGetUntyped (lens : Lens<'Model, Animator<'Model>>) (name : ^Name) (model : 'Model) =
            let sym = name |> symbol Unchecked.defaultof<NameConverter>
            let animator = model |> Optic.get lens
            animator.Animations |> HashMap.tryFind sym

    [<AutoOpen>]
    module private Implementation =

        [<AutoOpen>]
        module private Utility =

            let lensAnim =
                (fun (self : Animator<'Model>) -> self.Animations),
                (fun value (self : Animator<'Model>) -> { self with Animations = value })

            let animationLens (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) : Lens<'Model, IAnimation<'Model>> =
                let lens = lens >-> lensAnim
                (fun model -> model |> Optic.get lens |> HashMap.find name),
                (fun value model -> model |> Optic.map lens (HashMap.add name value))

            /// Updates a single animation
            let update (globalTime : GlobalTime) (animation : IAnimation<'Model>) =
                match animation.State with
                | State.Running startTime ->
                    let endTime = startTime + animation.TotalDuration

                    if globalTime > endTime then
                        animation.Perform <| Action.Update(endTime, true)
                    else
                        animation.Perform <| Action.Update(globalTime, false)
                | _ ->
                    animation

            /// Notifies animation observers.
            let commit (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
                let lens = name |> animationLens lens
                animation.Commit(lens, name, model)

            /// Update the given animation according to the given function and notifies its observers.
            let updateCommit (lens : Lens<'Model, Animator<'Model>>)
                             (name : Symbol) (mapping : IAnimation<'Model> -> IAnimation<'Model>)
                             (animation : IAnimation<'Model>) (model : 'Model) =

                let animation = mapping animation

                model
                |> Optic.map (lens >-> lensAnim) (HashMap.add name animation)
                |> commit lens name animation

            /// Alters an animation (if it exists) according to the given function and notifies its observers.
            /// The function returns the updated animation and a flag, indicating if it should be kept in the map or removed.
            let tryUpdateNotify (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (mapping : IAnimation<'Model> -> IAnimation<'Model>) (model : 'Model) =
                tryGetUntyped lens name model
                |> Option.map (fun anim -> (anim, model) ||> updateCommit lens name mapping)
                |> Option.defaultValue model

            /// Notifies the animation observers to compute a new model.
            let notifyAll (lens : Lens<'Model, Animator<'Model>>) (model : 'Model) =
                let animator =
                    model |> Optic.get lens

                (model, animator.Animations)
                ||> HashMap.fold (fun model name animation ->
                    model |> commit lens name animation
                )

        /// Sets the tick rate.
        let setTickRate (rate : int) (animator : Animator<'Model>) =
            if rate < 1 || rate > 1000 then
                Log.warn "Animation tick rate must be within [1, 1000] Hz"

            { animator with TickRate = rate |> clamp 1 1000 }

        /// Adds a new animation.
        let add (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (startFrom : float option) (animation : IAnimation<'Model>) (model : 'Model) =
            let mapping (animation : IAnimation<'Model>) =
                match startFrom with
                | Some t -> animation.Start(Time.get(), t)
                | _ -> animation

            (animation, model) ||> updateCommit lens name mapping

        /// Removes an animation.
        let remove (name : Symbol) (animator : Animator<'Model>) =
            { animator with Animations = animator.Animations |> HashMap.remove name }

        /// Stops an animation and removes it optionally.
        let stop (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (discard : bool) (model : 'Model) =
            model
            |> tryUpdateNotify lens name (fun a -> a.Stop())
            |> if discard then Optic.map lens (remove name) else id

        /// Starts or restarts the animation with the given name.
        let start (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (startFrom : float) (model : 'Model) =
            model |> tryUpdateNotify lens name (fun a -> a.Start(Time.get(), startFrom))

        /// Pauses the animation with the given name.
        let pause (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> tryUpdateNotify lens name (fun a -> a.Pause <| Time.get())

        /// Resumes the animation with the given name.
        let resume (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> tryUpdateNotify lens name (fun a -> a.Resume <| Time.get())

        /// Processes an animation tick and updates the model accoringly.
        let tick (lens : Lens<'Model, Animator<'Model>>) (model : 'Model) =

            let lensAnim = lens >-> lensAnim
            let lensTickCount= lens >-> Animator.TickCount_

            let update (animations : HashMap<Symbol, IAnimation<'Model>>) =
                animations |> HashMap.map (fun _ a -> a |> update (Time.get()))

            let remove (animations : HashMap<Symbol, IAnimation<'Model>>) =
                animations |> HashMap.filter (fun _ a -> not a.IsFinished)

            // Update all animations, notify observers, and remove finished animations
            model
            |> Optic.map lensAnim update
            |> notifyAll lens
            //|> Optic.map lensAnim remove
            |> Optic.map lensTickCount (fun x -> incr x; x)

    /// Processes animation messages.
    let update (msg : AnimatorMessage<'Model>) (model : 'Model) =
        let lens = Lenses.get<'Model>

        match msg with
        | AnimatorMessage.Tick ->
            model |> tick lens

        | AnimatorMessage.Set (name, animation, startFrom) ->
            model |> add lens name startFrom animation

        | AnimatorMessage.Remove name ->
            model |> Optic.map lens (remove name)

        | AnimatorMessage.Stop (name, remove) ->
            model |> stop lens name remove

        | AnimatorMessage.Start (name, startFrom) ->
            model |> start lens name startFrom

        | AnimatorMessage.Pause name ->
            model |> pause lens name

        | AnimatorMessage.Resume name ->
            model |> resume lens name

        | AnimatorMessage.SetTickRate rate ->
            model |> Optic.map lens (setTickRate rate)

    /// Adds or updates an animation with the given name.
    /// The name can be a string or Symbol.
    let inline setFrom (name : ^Name) (animation : IAnimation<'Model>) (startFrom : float) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Set(sym, animation, Some startFrom))

    /// Adds or updates an animation with the given name.
    /// The name can be a string or Symbol.
    let inline set (name : ^Name) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> setFrom name animation 0.0

    /// Adds an animation without a name.
    let inline add (animation : IAnimation<'Model>) (model : 'Model) =
        let sym = Name.get()
        model |> set sym animation

    /// Removes the animation with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline remove (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Remove sym)

    /// Stops the animation with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline stop (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Stop(sym, false))

    /// Starts or restarts the animation with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline start (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Start(sym, 0.0))

    /// Pauses the animation with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline pause (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Pause sym)

    /// Resumes the paused animation with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline resume (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Resume sym)

    /// Returns the current global time for all animators.
    let time() =
        Time.get()

    /// Creates an initial state for the animator.
    /// The lens is cached and used to update the manager in the containing model.
    let initial (lens : Lens<'Model, Animator<'Model>>) : Animator<'Model> =
        lens |> Lenses.set
        {
            Animations = HashMap.empty
            TickRate = 60
            TickCount = ref 0
        }

    /// Thread pool that generates tick messages in case no ticks have been processed.
    /// Tick messages are generated on demand, because optimally the animations are updated on Rendered messages.
    /// This thread pool makes sure animations are updated when the scene does not change (e.g. when starting or resuming animations).
    // NOTE: very naive, tick rate not accurate
    let threads (model : Animator<'Model>) =
        let timestep = 1000 / model.TickRate
        let mutable lastTick = !model.TickCount

        let rec time() =
            proclist {
                do! Proc.Sleep(timestep)

                let tick = !model.TickCount

                if lastTick = tick then
                    yield AnimatorMessage.Tick
                else
                    lastTick <- tick

                yield! time()
            }

        if model.Animations |> HashMap.exists (fun _ a -> a.IsRunning) then
            ThreadPool.add "animationTicks" (time()) ThreadPool.empty
        else
            ThreadPool.empty