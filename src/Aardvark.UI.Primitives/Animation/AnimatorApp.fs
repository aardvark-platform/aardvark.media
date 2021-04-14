namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

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

module Animator =
    open Aether
    open Aardvark.UI.Anewmation
    open InternalAnimatorUtilities

    // Global time for animations
    module private Time =
        open System.Diagnostics

        let private sw = Stopwatch.StartNew()

        let get() =
            sw.Elapsed.MicroTime |> GlobalTime.Timestamp

    [<AutoOpen>]
    module private Implementation =

        /// Processes an animation tick and updates the model accoringly.
        let tick (lens : Lens<'Model, Animator<'Model>>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            // Update all running animations
            let globalTime = Time.get()

            for a in animations do
                match a.State with
                | State.Running startTime ->
                    let action =
                        let endTime = startTime + a.TotalDuration

                        if globalTime > endTime then
                            Action.Update(endTime, true)
                        else
                            Action.Update(globalTime, false)

                    a.Perform action

                | _ -> ()

            // Notify all observers
            let mutable model = model

            for a in animations do
                model <- a.Commit(model)

            // Increase tick count
            model |> Optic.map lens (fun a -> inc &a.TickCount; a)

        let set (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations
            let instance = animation.Create(name)

            let rec loop i =
                if i >= animations.Count then animations.Add(instance) else
                if animations.[i].Name = name then animations.[i] <- instance else loop (i + 1)

            loop 0
            model

        let remove (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            let rec loop i =
                if i < animations.Count then
                    if animations.[i].Name = name then animations.RemoveAt i else loop (i + 1)

            loop 0
            model

        let private perform (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            let rec loop i =
                if i < animations.Count then
                    if animations.[i].Name = name then
                        action animations.[i]
                        animations.[i].Commit(model)
                    else
                        loop (i + 1)
                else
                    model

            loop 0

        let stop (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Stop())

        let start (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (startFrom : float) (restart : bool) (model : 'Model) =
            model |> perform lens name (fun a ->
                if restart || not a.IsRunning then
                    a.Start(Time.get(), startFrom)
            )

        let pause (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Pause <| Time.get())

        let resume (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Resume <| Time.get())

    /// Processes animation messages.
    let update (msg : AnimatorMessage<'Model>) (model : 'Model) =
        let lens = Lenses.get<'Model>

        match msg with
        | AnimatorMessage.Tick ->
            model |> tick lens

        | AnimatorMessage.Set (name, animation) ->
            model |> set lens name animation

        | AnimatorMessage.Remove name ->
            model |> remove lens name

        | AnimatorMessage.Stop name ->
            model |> stop lens name

        | AnimatorMessage.Start (name, startFrom, restart) ->
            model |> start lens name startFrom restart

        | AnimatorMessage.Pause name ->
            model |> pause lens name

        | AnimatorMessage.Resume name ->
            model |> resume lens name


    /// Adds or updates an animation with the given name.
    let set (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> update (AnimatorMessage.Set (name, animation))

    /// Removes the animation with the given name if it exists.
    let remove (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Remove name)

    /// Stops the animation with the given name if it exists.
    let stop (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Stop name)

    /// Starts the animation with the given name if it exists and it is not running.
    let start (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Start (name, 0.0, false))

    /// Starts the animation with the given name if it exists and it is not running.
    /// The animation is started from the given normalized position.
    let startFrom (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> update (AnimatorMessage.Start (name, startFrom, false))

    /// Starts or restarts the animation with the given name if it exists.
    let restart (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Start (name, 0.0, true))

    /// Starts or restarts the animation with the given name if it exists.
    /// The animation is started from the given normalized position.
    let restartFrom  (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> update (AnimatorMessage.Start (name, startFrom, true))

    /// Pauses the animation with the given name if it exists.
    let pause (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Pause name)

    /// Resumes the paused animation with the given name if it exists.
    let resume (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Resume name)

    /// Returns the current global time for all animators.
    let time() =
        Time.get()

    /// Creates an initial state for the animator.
    /// The lens is cached and used to update the manager in the containing model.
    let initial (lens : Lens<'Model, Animator<'Model>>) : Animator<'Model> =
        lens |> Lenses.set
        {
            Animations = List()
            TickRate = 60
            TickCount = 0
        }

    /// Thread pool that generates tick messages in case no ticks have been processed.
    /// Tick messages are generated on demand, because optimally the animations are updated on Rendered messages.
    /// This thread pool makes sure animations are updated when the scene does not change (e.g. when starting or resuming animations).
    // NOTE: very naive, tick rate not accurate
    let threads (model : Animator<'Model>) =
        let timestep = 1000 / model.TickRate
        let mutable lastTick = model.TickCount

        let rec time() =
            proclist {
                do! Proc.Sleep(timestep)

                if lastTick = model.TickCount then
                    yield AnimatorMessage.Tick
                else
                    lastTick <- model.TickCount

                yield! time()
            }

        if model.Animations.Exists (fun a -> a.IsRunning) then
            ThreadPool.add "animationTicks" (time()) ThreadPool.empty
        else
            ThreadPool.empty