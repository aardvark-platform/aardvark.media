namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

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
        let adjustLocalTime (localTime : LocalTime) =
            let d = animation.Duration
            if d.IsFinite then
                LocalTime.max (d * animation.DistanceTime(localTime))
            else
                localTime

        let adjustGlobalTime (globalTime : GlobalTime) =
            match animation.State with
            | State.Running startTime ->
                let localTime = globalTime |> LocalTime.relative startTime
                startTime + adjustLocalTime localTime
            | _ ->
                globalTime

        match action with
        | Action.Start (t, l)   -> Action.Start (t, l |> adjustLocalTime)
        | Action.Update (t, f)  -> Action.Update (adjustGlobalTime t, f)
        | _                     -> action


    let perform (segment : Segment) (duration : Duration) (state : State) (action : Action) : (IAnimation<'Model> -> Action) =

        let outOfBounds t =
            (t < segment.Start && segment.Start <> LocalTime.zero) ||
            (t > segment.End && segment.End <> LocalTime.max duration)

        // Relay actions to members, starting them if necessary
        let start (globalTime : GlobalTime) (groupLocalTime : LocalTime) =
            if outOfBounds groupLocalTime then
                Action.Stop
            else
                Action.Start (globalTime, groupLocalTime - segment.Start)

        let update (finalize : bool) (globalTime : GlobalTime) (groupLocalTime : LocalTime) (animation : IAnimation<'Model>) =
            if outOfBounds groupLocalTime then
                Action.Update (globalTime, true)
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
        Mapping : System.Func<'Model, IAnimation<'Model>[], 'Value>
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

    member x.Perform(action : Action) =
        let action = x |> GroupSemantics.applyDistanceTime action

        let perform (i : int) (a : IAnimation<'Model>) =
            let segment = GroupSemantics.Segment.ofDuration x.Members.[i].TotalDuration
            let action = a |> GroupSemantics.perform segment x.Duration x.State action
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

    member x.Subscribe(observer : IAnimationObserver<'Model, 'Value>) =
        { x with Observable = x.Observable |> Observable.subscribe observer }

    member x.UnsubscribeAll() =
        { x with
            Members = x.Members |> Array.map (fun a -> a.UnsubscribeAll())
            Observable = Observable.empty }

    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with
            Members = x.Members |> Array.map (fun a -> a.Unsubscribe(observer))
            Observable = x.Observable |> Observable.unsubscribe observer }

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
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.Value
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'Value>
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
              Mapping = System.Func<_,_,_> (fun _ -> ignore)
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, unit>

        /// Combines two animations into a concurrent group.
        let andAlso (x : IAnimation<'Model>) (y : IAnimation<'Model>) =
            concurrent [x; y]