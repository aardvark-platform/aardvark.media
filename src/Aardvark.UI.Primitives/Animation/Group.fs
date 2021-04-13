﻿namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether
open OptimizedClosures

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private GroupSemantics =

    [<Struct>]
    type Segment =
        { Start : LocalTime; End : LocalTime }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Segment =

        let create (s : LocalTime) (e : LocalTime) =
            { Start = s; End = e}

        let ofDuration (d : Duration) =
            { Start = LocalTime.zero; End = LocalTime.max d}


    let applyDistanceTime (action : Action) (animation : IAnimation<'Model>) =
        let apply (localTime : LocalTime) =
            let d = animation.Duration
            if d.IsFinite then
                LocalTime.max (d * animation.DistanceTime(localTime))
            else
                localTime

        match action with
        | Action.Start (globalTime, startFrom) ->
            Action.Start (globalTime, apply startFrom)

        | Action.Update (globalTime, finalize) ->
            match animation.State with
            | State.Running startTime ->
                let localTime = globalTime |> LocalTime.relative startTime
                Action.Update (startTime + apply localTime, finalize)

            | _ ->
                action

        | _ ->
            action


    let perform (segment : Segment) (bidirectional : bool) (duration : Duration)
                (state : State) (action : Action) : (IAnimation<'Model> -> Action) =

        let outOfBounds t =
            (t < segment.Start && segment.Start <> LocalTime.zero) ||
            (t > segment.End && segment.End <> LocalTime.max duration)

        let endTime groupLocalTime globalTime =
            let endLocalTime =
                if groupLocalTime < segment.Start && bidirectional then
                    segment.Start
                else
                    segment.End

            globalTime + (endLocalTime - groupLocalTime)

        // Relay actions to members, starting them if necessary
        let start (globalTime : GlobalTime) (groupLocalTime : LocalTime) =
            if outOfBounds groupLocalTime then
                Action.Stop
            else
                Action.Start (globalTime, groupLocalTime - segment.Start)

        let update (finalize : bool) (globalTime : GlobalTime) (groupLocalTime : LocalTime) (animation : IAnimation<'Model>) =
            if outOfBounds groupLocalTime then
                Action.Update (globalTime |> endTime groupLocalTime, true)
            else
                if animation.IsRunning then
                    Action.Update (globalTime, finalize)
                else
                    Action.Start (globalTime, groupLocalTime - segment.Start)


        match action, state with
        | Action.Start (globalTime, groupLocalTime), _ ->
            fun _ -> start globalTime groupLocalTime

        | Action.Update (globalTime, finalize), State.Running groupStartTime ->
            let groupLocalTime = globalTime |> LocalTime.relative groupStartTime
            update finalize globalTime groupLocalTime

        | _ ->
            fun _ -> action


type private ConcurrentGroup<'Model, 'Value> =
    {
        StateMachine : StateMachine<'Value>
        Members : IAnimation<'Model>[]
        Mapping : FSharpFunc<'Model, IAnimation<'Model>[], 'Value>
        DistanceTimeFunction : DistanceTimeFunction
        Observable : Observable<'Model, 'Value>
    }

    member x.Value = x.StateMachine.Holder.Value

    member x.State = x.StateMachine.Holder.State

    member x.Duration =
        x.Members |> Array.map (fun a -> a.TotalDuration) |> Array.max

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.DistanceTime(groupLocalTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(groupLocalTime / x.Duration)

    member x.Bidirectional =
        x.DistanceTimeFunction.Bidirectional

    member x.Perform(action : Action) =
        let action = x |> GroupSemantics.applyDistanceTime action

        let perform (i : int) (a : IAnimation<'Model>) =
            let segment = GroupSemantics.Segment.ofDuration x.Members.[i].TotalDuration
            let action = a |> GroupSemantics.perform segment x.Bidirectional x.Duration x.State action
            a.Perform(action)

        { x with
            Members = x.Members |> Array.mapi perform
            StateMachine = x.StateMachine |> StateMachine.enqueue action }

    member x.Scale(duration) =
        let s = duration / x.Duration

        let scale (a : IAnimation<'Model>) =
            a.Scale(if isFinite s && not a.Duration.IsZero then a.Duration * s else duration)

        { x with Members = x.Members |> Array.map scale }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    member x.Subscribe(event : EventType, callback : Symbol -> 'Value -> 'Model -> 'Model) =
        { x with Observable = x.Observable |> Observable.subscribe event callback }

    member x.UnsubscribeAll() =
        { x with
            Members = x.Members |> Array.map (fun a -> a.UnsubscribeAll())
            Observable = Observable.empty }

    member x.Commit(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        // Commit members
        let members, model =
            let arr : IAnimation<'Model>[] = Array.zeroCreate x.Members.Length

            let model =
                (model, x.Members) ||> Array.foldi (fun i model animation ->
                    let model = animation.Commit(lens, name, model)
                    let animation = model |> Optic.get lens
                    arr.[i] <- animation
                    model
                )

            arr, model

        // Process all actions, from oldest to newest
        let machine, events =
            let eval _ = x.Mapping.Invoke(model, members)
            x.StateMachine |> StateMachine.run eval

        // Notify observers about changes
        let model =
            model |> Optic.set lens (
                { x with
                    StateMachine = machine;
                    Members = members } :> IAnimation<'Model>
            )

        x.Observable |> Observable.notify name events model

    interface IAnimation<'Model> with
        member x.State = x.State
        member x.Duration = x.Duration
        member x.TotalDuration = x.TotalDuration
        member x.DistanceTime(localTime) = x.DistanceTime(localTime)
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Commit(lens, name, model) = x.Commit(lens, name, model)
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.Value
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(event, callback) = x.Subscribe(event, callback) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


[<AutoOpen>]
module AnimationGroupExtensions =

    module Animation =

        /// <summary>
        /// Creates a concurrent animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let concurrent (animations : IAnimation<'Model> seq) =
            let animations =
                animations |> Array.ofSeq

            if animations.Length = 0 then
                raise <| System.ArgumentException("Animation group cannot be empty")

            { StateMachine = StateMachine.initial
              Members = animations
              Mapping = FSharpFunc<_,_,_>.Adapt (fun _ -> ignore)
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, unit>

        /// Combines two animations into a concurrent group.
        let andAlso (x : IAnimation<'Model>) (y : IAnimation<'Model>) =
            concurrent [x; y]