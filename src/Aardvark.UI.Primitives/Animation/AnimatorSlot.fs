namespace Aardvark.UI.Anewmation

open Aardvark.Base

// Global time for animations
module private Time =
    open System.Diagnostics

    let private sw = Stopwatch.StartNew()

    let get() =
        sw.Elapsed.MicroTime |> GlobalTime.Timestamp

type private DelayedAnimation<'Model> =
    struct
        val Animation : 'Model -> IAnimation<'Model>
        val Action : GlobalTime -> IAnimationInstance<'Model> -> unit

        new (animation : 'Model -> IAnimation<'Model>, perform : GlobalTime -> IAnimationInstance<'Model> -> unit) =
            { Animation = animation; Action = perform }
    end

type AnimatorSlot<'Model>(name : Symbol, instance : IAnimationInstance<'Model>) =
    let mutable current = instance
    let queue = ArrayQueue<DelayedAnimation<'Model>>()

    /// The name of the animator slot.
    member x.Name = name

    /// The current animation instance in the slot.
    member x.Current = current

    /// The number of queued animations in the slot.
    member x.Pending = queue.Count

    /// Updates the current animation instance in the queue (if it is running).
    member internal x.Update() =
        let globalTime = Time.get()

        match current.State with
        | State.Running startTime ->
            let action =
                let endTime = startTime + current.TotalDuration

                if globalTime > endTime then
                    Action.Update(endTime, true)
                else
                    Action.Update(globalTime, false)

            current.Perform action

        | _ -> ()

    /// Performs the given action on the current animation instance.
    member internal x.Perform(action : GlobalTime -> IAnimationInstance<'Model> -> unit) =
        current |> action (Time.get())

    /// Commits the current animation instance and prepares the next in the queue if required.
    member internal x.Commit(model : 'Model) =
        let model = current.Commit(model)

        if current.IsFinished then
            match queue.Dequeue() with
            | (true, delayed) ->
                let animation = delayed.Animation model
                current <- animation.Create(name)
                x.Perform(delayed.Action)
                x.Commit(model)

            | _ -> model
        else
            model

    /// Creates an instance of the given animation and sets it as current.
    /// Pending instances are removed.
    member internal x.Set(animation : IAnimation<'Model>) =
        current <- animation.Create name
        queue.Clear()

    /// Enqueues the given animation.
    member internal x.Enqueue(animation : 'Model -> IAnimation<'Model>, action : GlobalTime -> IAnimationInstance<'Model> -> unit) =
        queue.Enqueue (DelayedAnimation (animation, action))