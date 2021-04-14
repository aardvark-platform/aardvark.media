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

module Animator =
    open Aether
    open Aether.Operators
    open Aardvark.UI.Anewmation

    // Global time for animations
    module private Time =
        open System.Diagnostics

        let private sw = Stopwatch.StartNew()

        let get() =
            sw.Elapsed.MicroTime |> GlobalTime.Timestamp

    [<AutoOpen>]
    module private Implementation =

        let lensAnimation (lens : Lens<'Model, Animator<'Model>>) (index : int) : Lens<'Model, IAnimation<'Model>> =
            (fun model -> (model |> Optic.get lens).Animations.[index].Animation),
            (fun value model -> (model |> Optic.get lens).Animations.[index].Animation <- value; model)

        /// Processes an animation tick and updates the model accoringly.
        let tick (lens : Lens<'Model, Animator<'Model>>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            // Update all running animations
            let globalTime = Time.get()

            for i in 0 .. animations.Count - 1 do
                let a = animations.[i].Animation

                match a.State with
                | State.Running startTime ->
                    let action =
                        let endTime = startTime + a.TotalDuration

                        if globalTime > endTime then
                            Action.Update(endTime, true)
                        else
                            Action.Update(globalTime, false)

                    animations.[i].Animation <- a.Perform action

                | _ -> ()

            // Notify all observers
            let mutable model = model

            for i in 0 .. animations.Count - 1 do
                let lens = i |> lensAnimation lens

                let name = animations.[i].Name
                let animation = animations.[i].Animation
                model <- animation.Commit(lens, name, model)

            model |> Optic.map lens (fun a -> inc &a.TickCount; a)

        let set (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            let rec loop i =
                if i >= animations.Count then animations.Add({ Name = name; Animation = animation }) else
                if animations.[i].Name = name then animations.[i].Animation <- animation else loop (i + 1)

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

        let private perform (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (action : IAnimation<'Model> -> IAnimation<'Model>) (model : 'Model) =
            let animator = model |> Optic.get lens
            let animations = animator.Animations

            let rec loop i =
                if i < animations.Count then
                    if animations.[i].Name = name then
                        let animation = action animations.[i].Animation
                        let lens = i |> lensAnimation lens
                        animations.[i].Animation <- animation
                        animation.Commit(lens, name, model)
                    else
                        loop (i + 1)
                else
                    model

            loop 0

        let stop (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Stop())

        let start (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (startFrom : float) (restart : bool) (model : 'Model) =
            model |> perform lens name (fun a ->
                if restart || not a.IsRunning then a.Start(Time.get(), startFrom)
                else a
            )

        let pause (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Pause <| Time.get())

        let resume (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
            model |> perform lens name (fun a -> a.Resume <| Time.get())

    /// Processes animation messages.
    let update (lens : Lens<'Model, Animator<'Model>>) (msg : AnimatorMessage<'Model>) (model : 'Model) =
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
    let set (lens : Lens<'Model, Animator<'Model>>)  (name : Symbol) (animation : IAnimation<'Model>) (model : 'Model) =
        model |> update lens (AnimatorMessage.Set (name, animation))

    /// Removes the animation with the given name if it exists.
    let remove (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Remove name)

    /// Stops the animation with the given name if it exists.
    let stop (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Stop name)

    /// Starts the animation with the given name if it exists and it is not running.
    let start (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Start (name, 0.0, false))

    /// Starts the animation with the given name if it exists and it is not running.
    /// The animation is started from the given normalized position.
    let startFrom (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> update lens (AnimatorMessage.Start (name, startFrom, false))

    /// Starts or restarts the animation with the given name if it exists.
    let restart (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Start (name, 0.0, true))

    /// Starts or restarts the animation with the given name if it exists.
    /// The animation is started from the given normalized position.
    let restartFrom (lens : Lens<'Model, Animator<'Model>>)  (name : Symbol) (startFrom : float) (model : 'Model) =
        model |> update lens (AnimatorMessage.Start (name, startFrom, true))

    /// Pauses the animation with the given name if it exists.
    let pause (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Pause name)

    /// Resumes the paused animation with the given name if it exists.
    let resume (lens : Lens<'Model, Animator<'Model>>) (name : Symbol) (model : 'Model) =
        model |> update lens (AnimatorMessage.Resume name)

    /// Returns the current global time for all animators.
    let time() =
        Time.get()

    /// Creates an initial state for the animator.
    /// The lens is cached and used to update the manager in the containing model.
    let initial (lens : Lens<'Model, Animator<'Model>>) : Animator<'Model> =
        //lens |> Lenses.set
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

        if model.Animations.Exists (fun a -> a.Animation.IsRunning) then
            ThreadPool.add "animationTicks" (time()) ThreadPool.empty
        else
            ThreadPool.empty