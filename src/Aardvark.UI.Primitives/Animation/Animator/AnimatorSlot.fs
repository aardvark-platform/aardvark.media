namespace Aardvark.UI.Animation

open Aardvark.Base

type internal DelayedAnimation<'Model> =
    struct
        val Animation : 'Model -> IAnimation<'Model>
        val Action : IAnimationInstance<'Model> -> unit

        new (animation : 'Model -> IAnimation<'Model>, perform : IAnimationInstance<'Model> -> unit) =
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
    member internal x.Update(tick : GlobalTime) =
        match current.State with
        | State.Running startTime ->
            let action =
                let localTime = tick |> LocalTime.relative startTime
                let endTime = LocalTime.max current.TotalDuration

                if localTime >= endTime then
                    Action.Update(endTime, true)
                else
                    Action.Update(localTime, false)

            current.Perform action

        | _ -> ()

    /// Performs the given action on the current animation instance.
    member internal x.Perform(action : IAnimationInstance<'Model> -> unit) =
        current |> action

    /// Commits the current animation instance and prepares the next in the queue if required.
    member internal x.Commit(model : 'Model, tick : GlobalTime) =
        let model = current.Commit(model, tick)

        if current.IsFinished then
            match queue.Dequeue() with
            | true, delayed ->
                let animation = delayed.Animation model
                current <- animation.Create(name)
                x.Perform(delayed.Action)
                x.Commit(model, tick)

            | _ -> model
        else
            model

    /// Commits the current animation instance and prepares the next in the queue if required.
    member internal x.Commit(model : 'Model, tick : ValueOption<GlobalTime> inref) =
        match tick with
        | ValueSome t -> x.Commit(model, t)
        | _ -> model

    /// Creates an instance of the given animation and sets it as current.
    /// Pending instances are removed.
    member internal x.Set(animation : IAnimation<'Model>) =
        current <- animation.Create name
        queue.Clear()

    /// Enqueues the given animation.
    member internal x.Enqueue(animation : 'Model -> IAnimation<'Model>, action : IAnimationInstance<'Model> -> unit) =
        queue.Enqueue (DelayedAnimation (animation, action))