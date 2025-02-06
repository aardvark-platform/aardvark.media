namespace Aardvark.UI.Animation

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
    open Aardvark.UI.Animation
    open InternalAnimatorUtilities
    open InternalAnimatorUtilities.Converters

    // Global time for animations
    module private Time =
        open System.Diagnostics

        let private sw = Stopwatch.StartNew()

        let get() =
            sw.Elapsed.MicroTime |> GlobalTime.Timestamp

    [<AutoOpen>]
    module private Implementation =

        let inline untyped (animation : #IAnimation<'Model>) =
            animation :> IAnimation<'Model>

        let tick (lens : Lens<'Model, Animator<'Model>>) (time : GlobalTime) (model : 'Model) =

            // Set the current tick
            let animator = { Optic.get lens model with CurrentTick = ValueSome time }
            let mutable model = model |> Optic.set lens animator

            // Update all running animations
            for (_, s) in animator.Slots do
                s.Update time

            // Notify all observers
            for (_, s) in animator.Slots do
                model <- s.Commit(model, time)

            // Reset current tick and increase tick count
            model |> Optic.map lens (fun animator ->
                inc &animator.TickCount
                { animator with CurrentTick = ValueNone }
            )

        let set (lens : Lens<'Model, Animator<'Model>>)
                (name : Symbol) (animation : IAnimation<'Model>)
                (action : IAnimationInstance<'Model> -> unit)
                (model : 'Model) =

            let animator = model |> Optic.get lens

            match animator.Slots |> HashMap.tryFind name with
            | Some slot ->
                slot.Set(animation)
                slot.Perform(action)
                slot.Commit(model, &animator.CurrentTick)

            | _ ->
                let slot = AnimatorSlot(name, animation.Create name)
                slot.Perform(action)
                slot.Commit(model, &animator.CurrentTick)
                |> Optic.map lens (fun animator ->
                    { animator with Slots = animator.Slots |> HashMap.add name slot }
                )

        let enqueue (lens : Lens<'Model, Animator<'Model>>)
                    (name : Symbol) (animation : 'Model -> IAnimation<'Model>)
                    (action : IAnimationInstance<'Model> -> unit)
                    (model : 'Model) =

            let animator = model |> Optic.get lens

            match animator.Slots |> HashMap.tryFind name with
            | Some slot ->
                slot.Enqueue(animation, action)
                slot.Commit(model, &animator.CurrentTick)

            | _ ->
                let slot = AnimatorSlot(name, (animation model).Create name)
                slot.Perform(action)
                slot.Commit(model, &animator.CurrentTick)
                |> Optic.map lens (fun animator ->
                    { animator with Slots = animator.Slots |> HashMap.add name slot }
                )

        let remove (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> Optic.map lens (fun animator ->
                { animator with Slots = animator.Slots |> HashMap.remove name }
            )

        let perform (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
            let animator = model |> Optic.get lens

            match animator.Slots |> HashMap.tryFind name with
            | Some slot ->
                slot.Perform(action)
                slot.Commit(model, &animator.CurrentTick)

            | _ ->
                model

        let iterate (lens : Lens<'Model, Animator<'Model>>) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
            let animator = model |> Optic.get lens

            for (_, s) in animator.Slots do
                s.Perform(action)

            let mutable model = model

            for (_, s) in animator.Slots do
                model <- s.Commit(model, &animator.CurrentTick)

            model

        let filter (lens : Lens<'Model, Animator<'Model>>) (predicate : AnimatorSlot<'Model> -> bool) (model : 'Model) =
            model |> Optic.map lens (fun animator ->
                { animator with Slots = animator.Slots |> HashMap.filter (fun _ s -> predicate s) }
            )

    /// Processes animation messages.
    let update (msg : AnimatorMessage<'Model>) (model : 'Model) =
        let lens = Lenses.get<'Model>

        match msg with
        | AnimatorMessage.Tick time ->
            model |> tick lens time

        | AnimatorMessage.RealTimeTick ->
            let time = Time.get()
            model |> tick lens time

        | AnimatorMessage.Set (name, animation, action) ->
            model |> set lens name animation action

        | AnimatorMessage.Enqueue (name, animation, action) ->
            model |> enqueue lens name animation action

        | AnimatorMessage.Perform (name, action) ->
            model |> perform lens name action

        | AnimatorMessage.Remove name ->
            model |> remove lens name

        | AnimatorMessage.Iterate action ->
            model |> iterate lens action

        | AnimatorMessage.Filter predicate ->
            model |> filter lens predicate

    /// Performs an animation tick using the current time.
    let tick (time : GlobalTime) (model : 'Model) =
        model |> update (AnimatorMessage.Tick time)

    /// Performs an animation tick using the current time.
    let realTimeTick (model : 'Model) =
        model |> update AnimatorMessage.RealTimeTick

    /// Performs the action for the current animation instance of every slot.
    let iterate (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
        model |> update (AnimatorMessage.Iterate action)

    /// Removes every animation slot for which the given predicate returns false.
    let filter (predicate : AnimatorSlot<'Model> -> bool) (model : 'Model) =
        model |> update (AnimatorMessage.Filter predicate)

    /// Creates an animation instance for the slot with the given name, and performs the given action.
    /// Replaces any existing instances (current and queued) in the given slot.
    /// The name can be a string or Symbol.
    let inline createAndPerform (name : ^Name) (animation : IAnimation<'Model>) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Set (sym, animation, action))

    /// Creates an animation instance for the slot with the given name.
    /// Replaces any existing instances (current and queued) in the given slot.
    /// The name can be a string or Symbol.
    let inline create (name : ^Name) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerform name animation ignore

    /// Creates and starts an animation instance for the slot with the given name.
    /// Replaces any existing instances (current and queued) in the given slot.
    /// The name can be a string or Symbol.
    let inline createAndStart (name : ^Name) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerform name animation (fun a -> a.Start())

    /// Creates and starts an animation instance for the slot with the given name.
    /// The animation is started from the given normalized position.
    /// Replaces any existing instances (current and queued) in the given slot.
    /// The name can be a string or Symbol.
    let inline createAndStartFrom (name : ^Name) (animation : IAnimation<'Model>) (startFrom : float) (model : 'Model) =
        model |> createAndPerform name animation (fun a -> a.Start startFrom)

    /// Creates and starts an animation instance for the slot with the given name.
    /// The animation is started from the given local time.
    /// Replaces any existing instances (current and queued) in the given slot.
    /// The name can be a string or Symbol.
    let inline createAndStartFromLocal (name : ^Name) (animation : IAnimation<'Model>) (startFrom : LocalTime) (model : 'Model) =
        model |> createAndPerform name animation (fun a -> a.Start startFrom)

    /// Enqueues an animation in the slot with the given name.
    /// When all the previous instances in the slot have finished, the animation is computed, instantiated and the given action is performed.
    /// The name can be a string or Symbol.
    let inline createAndPerformDelayed (name : ^Name) (animation : 'Model -> #IAnimation<'Model>) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Enqueue (sym, animation >> untyped, action))

    /// Enqueues an animation in the slot with the given name.
    /// When all the previous instances in the slot have finished, the animation is computed and instantiated.
    /// The name can be a string or Symbol.
    let inline createDelayed (name : ^Name) (animation : 'Model -> #IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerformDelayed name animation ignore

    /// Enqueues an animation in the slot with the given name.
    /// When all the previous instances in the slot have finished, the animation is computed, instantiated and started.
    /// The name can be a string or Symbol.
    let inline createAndStartDelayed (name : ^Name) (animation : 'Model -> #IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerformDelayed name animation (fun a -> a.Start())

    /// Enqueues an animation in the slot with the given name.
    /// When all the previous instances in the slot have finished, the animation is computed, instantiated and started.
    /// The animation is started from the given normalized position.
    /// The name can be a string or Symbol.
    let inline createAndStartFromDelayed (name : ^Name) (animation : 'Model -> #IAnimation<'Model>) (startFrom : float) (model : 'Model) =
        model |> createAndPerformDelayed name animation (fun a -> a.Start startFrom)

    /// Enqueues an animation in the slot with the given name.
    /// When all the previous instances in the slot have finished, the animation is computed, instantiated and started.
    /// The animation is started from the given local time.
    /// The name can be a string or Symbol.
    let inline createAndStartFromLocalDelayed (name : ^Name) (animation : 'Model -> #IAnimation<'Model>) (startFrom : LocalTime) (model : 'Model) =
        model |> createAndPerformDelayed name animation (fun a -> a.Start startFrom)

    /// Performs the action for the current animation instance in the slot with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline perform (name : ^Name) (action : IAnimationInstance<'Model> -> unit) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Perform (sym, action))

    /// Removes the animation slot with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline remove (name : ^Name) (model : 'Model) =
        let sym = name |> symbol Unchecked.defaultof<NameConverter>
        model |> update (AnimatorMessage.Remove sym)

    /// Removes all animation slots.
    let removeAll (model : 'Model) =
        model |> filter (fun _ -> false)

    /// Removes all finished animation slots.
    let removeFinished (model : 'Model) =
        model |> filter (fun s -> not s.Current.IsFinished)

    /// Stops the current animation instance in the slot with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline stop (name : ^Name) (model : 'Model) =
        model |> perform name (fun a -> a.Stop())

    /// Starts the current animation instance in the slot with the given name if it exists and it is not running or paused.
    /// The name can be a string or Symbol.
    let inline start (name : ^Name) (model : 'Model) =
        model |> perform name (fun a ->
            if a.IsStopped || a.IsFinished then a.Start()
        )

    /// Starts or resumes the current animation instance in the slot with the given name if it exists and it is not running.
    /// The name can be a string or Symbol.
    let inline startOrResume (name : ^Name) (model : 'Model) =
        model |> perform name (fun a ->
            if a.IsPaused then a.Resume()
            elif a.IsStopped || a.IsFinished then a.Start()
        )

    /// Starts the current animation instance in the slot with the given name if it exists and it is not running or paused.
    /// The animation is started from the given normalized position.
    /// The name can be a string or Symbol.
    let inline startFrom (name : ^Name) (startFrom : float) (model : 'Model) =
        model |> perform name (fun a ->
            if a.IsStopped || a.IsFinished then a.Start startFrom
        )

    /// Starts the current animation instance in the slot with the given name if it exists and it is not running or paused.
    /// The animation is started from the given local time.
    /// The name can be a string or Symbol.
    let inline startFromLocal (name : ^Name) (startFrom : LocalTime) (model : 'Model) =
        model |> perform name (fun a ->
            if a.IsStopped || a.IsFinished then a.Start startFrom
        )

    /// Starts or restarts the current animation instance in the slot with the given name if it exists.
    /// The name can be a string or Symbol.
    let inline restart (name : ^Name) (model : 'Model) =
        model |> perform name (fun a -> a.Start())

    /// Starts or restarts the current animation instance in the slot with the given name if it exists.
    /// The animation is started from the given normalized position.
    /// The name can be a string or Symbol.
    let inline restartFrom (name : ^Name) (startFrom : float) (model : 'Model) =
        model |> perform name (fun a -> a.Start startFrom)

    /// Starts or restarts the current animation instance in the slot with the given name if it exists.
    /// The animation is started from the given local time.
    /// The name can be a string or Symbol.
    let inline restartFromLocal (name : ^Name) (startFrom : LocalTime) (model : 'Model) =
        model |> perform name (fun a -> a.Start startFrom)

    /// Pauses the current animation instance in the slot with the given name if it exists and it is running.
    /// The name can be a string or Symbol.
    let inline pause (name : ^Name) (model : 'Model) =
        model |> perform name (fun a -> a.Pause())

    /// Resumes the current animation instance in the slot with the given name if it exists and it is paused.
    /// The name can be a string or Symbol.
    let inline resume (name : ^Name) (model : 'Model) =
        model |> perform name (fun a -> a.Resume())

    /// Creates an initial state for the animator.
    /// The lens is cached and used to update the manager in the containing model.
    let initial (lens : Lens<'Model, Animator<'Model>>) : Animator<'Model> =
        lens |> Lenses.set
        {
            Slots = HashMap.empty
            TickRate = 60
            CurrentTick = ValueNone
            TickCount = 0
        }

    /// Thread pool that generates real-time tick messages in case no ticks have been processed.
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
                    yield AnimatorMessage.RealTimeTick
                else
                    lastTick <- model.TickCount

                yield! time()
            }

        ThreadPool.add "animationTicks" (time()) ThreadPool.empty