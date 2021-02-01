namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open Aether
open System

module private GroupState =

    let private priority = function
        | State.Running _ -> 0
        | State.Paused _ -> 1
        | State.Finished -> 2
        | State.Stopped -> 3

    let get (animations : State list) =
        animations |> List.sortBy priority |> List.head

    let running (animations : State list) =
        animations |> List.choose (function
            | State.Running t -> Some t
            | _ -> None
        )
        |> List.sort
        |> List.tryHead

type private Group<'Model, 'Value> =
    {
        /// Current state of the group.
        States : State list

        /// Animations contained in the group.
        Members : IAnimation<'Model> list

        /// Distance-time function of the group.
        DistanceTimeFunction : DistanceTimeFunction

        /// Observers to be notified of changes.
        Observers : HashMap<IAnimationObserver<'Model>, IAnimationObserver<'Model, 'Value>>
    }

    /// Returns the value of the animation.
    member x.Value = Unchecked.defaultof<'Value>

    /// Returns the state of the animation.
    member x.State = GroupState.get x.States

    /// Returns the duration of the group.
    member x.Duration =
        x.DistanceTimeFunction.Duration

    /// Stops the animation and resets it.
    member x.Stop() =
        { x with Members = x.Members |> List.map (fun a -> a.Stop()) }

    /// Starts the animation from the beginning (i.e. sets its start time to the given global time).
    member x.Start(globalTime) =
        { x with Members = x.Members |> List.map (fun a -> a.Start(globalTime)) }

    /// Pauses the animation if it is running or has started.
    member x.Pause(globalTime) =
        { x with Members = x.Members |> List.map (fun a -> a.Pause(globalTime)) }

    /// Resumes the animation from the point it was paused.
    /// Has no effect if the animation is not paused.
    member x.Resume(globalTime) =
        { x with Members = x.Members |> List.map (fun a -> a.Resume(globalTime)) }

    /// Updates the animation to the given global time.
    member x.Update(globalTime) =
        match GroupState.running x.States with
        | Some startTimeGroup ->
            let f, t = x.DistanceTimeFunction.Invoke(globalTime - startTimeGroup)
            let adjustedGlobalTime = if f then globalTime else startTimeGroup + t * x.Duration

            { x with Members = x.Members |> List.map (fun a -> a.Update(adjustedGlobalTime)) }
        | _ ->
            x

    member x.Scale(duration) =
        let s = duration / x.Duration

        let scale (a : IAnimation<'Model>) =
            a.Scale(if isFinite s && not a.Duration.IsZero then a.Duration * s else duration)

        { x with
            Members = x.Members |> List.map scale
            DistanceTimeFunction = x.DistanceTimeFunction.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    /// Registers a new observer.
    member x.Subscribe(observer : IAnimationObserver<'Model, 'Value>) =
        if not observer.IsEmpty then
            let key = observer :> IAnimationObserver<'Model>
            { x with Observers = x.Observers |> HashMap.add key observer }
        else
            x

    /// Removes all observers.
    member x.UnsubscribeAll() =
        { x with
            Members = x.Members |> List.map (fun a -> a.UnsubscribeAll())
            Observers = HashMap.empty }

    /// Removes the given observer (if present).
    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with
            Members = x.Members |> List.map (fun a -> a.Unsubscribe(observer))
            Observers = x.Observers |> HashMap.remove observer }

    /// Notifies all observers, invoking the respective callbacks.
    /// Returns the model computed by the callbacks.
    member x.Notify(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        let members, model =
            (([], model), x.Members) ||> Seq.fold (fun (list, model) animation ->
                let model = animation.Notify(lens, name, model)
                let animation = model |> Optic.get lens
                animation :: list, model
            )

        let next = { x with Members = members; States = members |> List.map (fun a -> a.State) }
        let model = model |> Optic.set lens (next :> IAnimation<'Model>)

        let events =
            let perMember = (x.States, next.States) ||> List.map2 Events.compute
            let perGroup = next.States |> GroupState.get |> Events.compute x.State

            perGroup
            |> List.filter (function
                | EventType.Start -> perMember |> List.forall (List.contains EventType.Start)   // Group only starts if ALL members start
                | _ -> true
            )
            |> List.sort

        let notify model event =
            (model, x.Observers |> HashMap.toSeq)
            ||> Seq.fold (fun model (_, obs) -> obs.OnNext(model, name, event, next.Value))

        (model, events) ||> Seq.fold notify

    interface IAnimation<'Model> with
        member x.State = x.State
        member x.Duration = x.Duration
        member x.Stop() = x.Stop() :> IAnimation<'Model>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Notify(lens, name, model) = x.Notify(lens, name, model)
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.Value
        member x.Stop() = x.Stop() :> IAnimation<'Model, 'Value>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model, 'Value>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model, 'Value>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model, 'Value>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'Value>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


[<AutoOpen>]
module AnimationGroupExtensions =

    module Animation =

        /// <summary>
        /// Creates a parallel animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let group (animations : #IAnimation<'Model> seq) =
            let animations =
                animations |> List.ofSeq |> List.map (fun a -> a :> IAnimation<'Model>)

            if List.isEmpty animations then
                raise <| ArgumentException("Animation group cannot be empty")

            let duration =
                animations |> List.map (fun a -> a.Duration) |> List.sortDescending |> List.head

            { States = animations |> List.map (fun a -> a.State)
              Members = animations
              DistanceTimeFunction = { DistanceTimeFunction.empty with Duration = duration }
              Observers = HashMap.empty } :> IAnimation<'Model, unit>

        /// Combines two animations into a parallel group.
        let andAlso (x : IAnimation<'Model>) (y : IAnimation<'Model>) =
            group [x; y]