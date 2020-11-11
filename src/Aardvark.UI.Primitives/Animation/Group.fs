namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open Aether

module private GroupState =

    let private priority = function
        | State.Running _ -> 0
        | State.Paused _ -> 1
        | State.Finished -> 2
        | State.Stopped -> 3

    let get (animations : State list) =
        animations |> List.sortBy priority |> List.head

type private Group<'Model> =
    {
        /// Current state of the group.
        States : State list

        /// Animations contained in the group.
        Members : IAnimation<'Model> list

        /// Observers to be notified of changes.
        Observers : HashSet<IObserver<'Model, unit>>
    }

    /// Returns the state of the animation.
    member x.State = GroupState.get x.States

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
        { x with Members = x.Members |> List.map (fun a -> a.Update(globalTime)) }

    /// Updates the distance time function of the animation.
    member x.UpdateDistanceTimeFunction(mapping : IDistanceTimeFunction -> IDistanceTimeFunction) =
        { x with Members = x.Members |> List.map (fun a -> a.UpdateDistanceTimeFunction(mapping)) }

    /// Registers a new observer.
    member x.Subscribe(observer : IObserver<'Model, unit>) =
        if not observer.IsEmpty then
            { x with Observers = x.Observers |> HashSet.add observer }
        else
            x

    /// Removes all observers.
    member x.UnsubscribeAll() =
        { x with Observers = HashSet.empty }

    /// Removes the given observer (if present).
    member x.Unsubscribe(observer : IObserver<'Model, unit>) =
        { x with Observers = x.Observers |> HashSet.remove observer }

    /// Notifies all observers, invoking the respective callbacks.
    /// Returns the model computed by the callbacks.
    member x.Notify(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        let members, model =
            (([], model), x.Members) ||> Seq.fold (fun (list, model) animation ->
                let model = animation.Notify(lens, name, model)
                let animation = model |> Optic.get lens
                animation :: list, model
            )

        let states = members |> List.map (fun a -> a.State)
        let model = model |> Optic.set lens ({ x with Members = members; States = states } :> IAnimation<'Model>)

        let events =
            let perMember = (x.States, states) ||> List.map2 Events.compute
            let perGroup = states |> GroupState.get |> Events.compute x.State

            perGroup
            |> List.filter (function
                | EventType.Start -> perMember |> List.forall (List.contains EventType.Start)   // Group only starts if ALL members start
                | _ -> true
            )
            |> List.sort

        let notify model event =
            (model, x.Observers)
            ||> HashSet.fold (fun model obs -> obs.OnNext(model, name, event, ()))

        (model, events) ||> Seq.fold notify

    interface IAnimation<'Model, unit> with
        member x.State = x.State
        member x.Stop() = x.Stop() :> IAnimation<'Model>
        member x.Start(globalTime) = x.Start(globalTime) :> IAnimation<'Model>
        member x.Pause(globalTime) = x.Pause(globalTime) :> IAnimation<'Model>
        member x.Resume(globalTime) = x.Resume(globalTime) :> IAnimation<'Model>
        member x.Update(globalTime) = x.Update(globalTime) :> IAnimation<'Model>
        member x.UpdateDistanceTimeFunction(f) = x.UpdateDistanceTimeFunction(f) :> IAnimation<'Model>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, unit>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, unit>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, unit>
        member x.Notify(lens, name, model) = x.Notify(lens, name, model)
        member x.UpdateSpaceFunction(f) = x :> IAnimation<'Model, unit>


[<AutoOpen>]
module AnimationGroupExtensions =

    module Animation =

        open System

        /// <summary>
        /// Creates an animation group from a list of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the list is empty.</exception>
        let ofList (animations : IAnimation<'Model> list) =
            if List.isEmpty animations then
                raise <| ArgumentException("Animation group cannot be empty")

            { States = animations |> List.map (fun a -> a.State)
              Members = animations
              Observers = HashSet.empty } :> IAnimation<'Model, unit>

        /// <summary>
        /// Creates an animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let ofSeq (animations : IAnimation<'Model> list) =
            animations |> Seq.toList |> ofList

        /// <summary>
        /// Creates an animation group from an array of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the array is empty.</exception>
        let ofArray (animations : IAnimation<'Model>[]) =
            animations |> Array.toList |> ofList

        /// Combines two animations.
        let andAlso (x : IAnimation<'Model>) (y : IAnimation<'Model>) =
            ofList [x; y]