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

        let tick (lens : Lens<'Model, Animator<'Model>>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            // Update all running animations
            let globalTime = Time.get()

            for (_, a) in animations do
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

            for (_, a) in animations do
                model <- a.Commit(model)

            // Increase tick count
            model |> Optic.map lens (fun a -> inc &a.TickCount; a)

        let create (lens : Lens<'Model, Animator<'Model>>)
                   (name : Symbol)
                   (animation : IAnimation<'Model>)
                   (action : GlobalTime -> IAnimationInstance<'Model> -> unit)
                   (model : 'Model) =

            let instance = animation.Create(name)

            let globalTime = Time.get()
            instance |> action globalTime

            instance.Commit(model)
            |> Optic.map lens (fun animator ->
                { animator with Animations = animator.Animations |> HashMap.add name instance }
            )

        let remove (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> Optic.map lens (fun animator ->
                { animator with Animations = animator.Animations |> HashMap.remove name }
            )

        let perform (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (action : GlobalTime -> IAnimationInstance<'Model> -> unit) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            match animations |> HashMap.tryFind name with
            | Some inst ->
                let globalTime = Time.get()
                inst |> action globalTime
                inst.Commit(model)

            | _ ->
                model

        let iterate (lens : Lens<'Model, Animator<'Model>>) (action : GlobalTime -> IAnimationInstance<'Model> -> unit) (model : 'Model) =
            let animator = model |> Optic.get lens
            let globalTime = Time.get()

            for (_, a) in animator.Animations do
                a |> action globalTime

            let mutable model = model

            for (_, a) in animator.Animations do
                model <- a.Commit(model)

            model

        let filter (lens : Lens<'Model, Animator<'Model>>) (predicate : IAnimationInstance<'Model> -> bool) (model : 'Model) =
            model |> Optic.map lens (fun animator ->
                { animator with Animations = animator.Animations |> HashMap.filter (fun _ inst -> predicate inst) }
            )

    /// Processes animation messages.
    let update (msg : AnimatorMessage<'Model>) (model : 'Model) =
        let lens = Lenses.get<'Model>

        match msg with
        | AnimatorMessage.Tick ->
            model |> tick lens

        | AnimatorMessage.Create (name, animation, action) ->
            model |> create lens name animation action

        | AnimatorMessage.Perform (name, action) ->
            model |> perform lens name action

        | AnimatorMessage.Remove name ->
            model |> remove lens name

        | AnimatorMessage.Iterate action ->
            model |> iterate lens action

        | AnimatorMessage.Filter predicate ->
            model |> filter lens predicate


    /// Performs the action for every animation instance.
    let iterate (action : GlobalTime -> IAnimationInstance<'Model> -> unit) (model : 'Model) =
        model |> update (AnimatorMessage.Iterate action)

    /// Removes every animation instance for which the given predicate returns false.
    let filter (predicate : IAnimationInstance<'Model> -> bool) (model : 'Model) =
        model |> update (AnimatorMessage.Filter predicate)

    /// Creates an instance of the given animation with the given name, and performs the given action.
    /// Replaces any existing instance with the given name.
    let createAndPerform (name : Symbol) (animation : IAnimation<'Model>) (action : GlobalTime -> IAnimationInstance<'Model> -> unit) (model : 'Model) =
        model |> update (AnimatorMessage.Create (name, animation, action))

    /// Creates an instance of the given animation with the given name.
    /// Replaces any existing instance with the given name.
    let create (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerform name animation (fun _ -> ignore)

    /// Creates and starts an instance of the given animation with the given name.
    /// Replaces any existing instance with the given name.
    let createAndStart (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> createAndPerform name animation (fun t a -> a.Start(t))

    /// Creates and starts an instance of the given animation with the given name.
    /// The animation is started from the given normalized position.
    /// Replaces any existing instance with the given name.
    let createAndStartFrom (name : Symbol) (animation : IAnimation<'Model>) (startFrom : float) (model : 'Model) =
        model |> createAndPerform name animation (fun t a -> a.Start(t, startFrom))

    /// Creates and starts an instance of the given animation with the given name.
    /// The animation is started from the given local time.
    /// Replaces any existing instance with the given name.
    let createAndStartFromLocal (name : Symbol) (animation : IAnimation<'Model>) (startFrom : LocalTime) (model : 'Model) =
        model |> createAndPerform name animation (fun t a -> a.Start(t, startFrom))

    /// Performs the action for the animation instance with given name if it exists.
    let perform (name : Symbol) (action : GlobalTime -> IAnimationInstance<'Model> -> unit) (model : 'Model) =
        model |> update (AnimatorMessage.Perform (name, action))

    /// Removes the animation instance with the given name if it exists.
    let remove (name : Symbol) (model : 'Model) =
        model |> update (AnimatorMessage.Remove name)

    /// Removes all animation instances.
    let removeAll (model : 'Model) =
        model |> filter (fun _ -> true)

    /// Removes all finished animation instances.
    let removeFinished (model : 'Model) =
        model |> filter (fun a -> a.IsFinished)

    /// Stops the animation instance with the given name if it exists.
    let stop (name : Symbol) (model : 'Model) =
        model |> perform name (fun _ a -> a.Stop())

    /// Starts the animation instance with the given name if it exists and it is not running or paused.
    let start (name : Symbol) (model : 'Model) =
        model |> perform name (fun t a ->
            if a.IsStopped || a.IsFinished then a.Start(t)
        )

    /// Starts or resumes the animation instance with the given name if it exists and it is not running.
    let startOrResume (name : Symbol) (model : 'Model) =
        model |> perform name (fun t a ->
            if a.IsPaused then a.Resume(t)
            elif a.IsStopped || a.IsFinished then a.Start(t)
        )

    /// Starts the animation instance with the given name if it exists and it is not running or paused.
    /// The animation is started from the given normalized position.
    let startFrom (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> perform name (fun t a ->
            if a.IsStopped || a.IsFinished then a.Start(t, startFrom)
        )

    /// Starts the animation instance with the given name if it exists and it is not running or paused.
    /// The animation is started from the given local time.
    let startFromLocal (name : Symbol) (startFrom : LocalTime) (model : 'Model) =
        model |> perform name (fun t a ->
            if a.IsStopped || a.IsFinished then a.Start(t, startFrom)
        )

    /// Starts or restarts the animation instance with the given name if it exists.
    let restart (name : Symbol) (model : 'Model) =
        model |> perform name (fun t a -> a.Start(t))

    /// Starts or restarts the animation instance with the given name if it exists.
    /// The animation is started from the given normalized position.
    let restartFrom (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> perform name (fun t a -> a.Start(t, startFrom))

    /// Starts or restarts the animation instance with the given name if it exists.
    /// The animation is started from the given local time.
    let restartFromLocal (name : Symbol) (startFrom : LocalTime) (model : 'Model) =
        model |> perform name (fun t a -> a.Start(t, startFrom))

    /// Pauses the animation instance with the given name if it exists.
    let pause (name : Symbol) (model : 'Model) =
        model |> perform name (fun t a -> a.Pause(t))

    /// Resumes the paused animation instance with the given name if it exists.
    let resume (name : Symbol) (model : 'Model) =
        model |> perform name (fun t a -> a.Resume(t))

    /// Creates an initial state for the animator.
    /// The lens is cached and used to update the manager in the containing model.
    let initial (lens : Lens<'Model, Animator<'Model>>) : Animator<'Model> =
        lens |> Lenses.set
        {
            Animations = HashMap.empty
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

        if model.Animations |> HashMap.exists (fun _ a -> a.IsRunning) then
            ThreadPool.add "animationTicks" (time()) ThreadPool.empty
        else
            ThreadPool.empty