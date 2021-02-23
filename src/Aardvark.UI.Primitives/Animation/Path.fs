namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

type private Adapter<'Model, 'Value> =
    {
        StateMachine : StateMachine<'Value>
        Animation : IAnimation<'Model>
        Observable : Observable<'Model, 'Value>
    }

    member x.Perform(action) =
        { x with
            StateMachine = x.StateMachine |> StateMachine.enqueue action
            Animation = x.Animation.Perform(action) }

    member x.Scale(duration) =
        { x with Animation = x.Animation.Scale(duration) }

    member x.Ease(easing, compose) =
        { x with Animation = x.Animation.Ease(easing, compose) }

    member x.Loop(iterations, mode) =
        { x with Animation = x.Animation.Loop(iterations, mode) }

    member x.Subscribe(observer : IAnimationObserver<'Model, 'Value>) =
        { x with Observable = x.Observable |> Observable.subscribe observer }

    member x.UnsubscribeAll() =
        { x with
            Animation = x.Animation.UnsubscribeAll()
            Observable = Observable.empty }

    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        { x with
            Animation = x.Animation.Unsubscribe(observer)
            Observable = x.Observable |> Observable.unsubscribe observer }

    member x.Commit(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =

        // Commit wrapped animation and input
        let animation, model =
            let model = x.Animation.Commit(lens, name, model)
            let animation = model |> Optic.get lens
            animation,  model

        // Process all actions, from oldest to newest
        let machine, events =
            let eval = fun _ -> Unchecked.defaultof<'Value>
            x.StateMachine |> StateMachine.run eval

        // Notify observers about changes
        let model =
            model |> Optic.set lens (
                { x with
                    StateMachine = machine;
                    Animation = animation } :> IAnimation<'Model>
            )

        x.Observable |> Observable.notify name events model

    interface IAnimation<'Model> with
        member x.State = x.StateMachine.Holder.State
        member x.Duration = x.Animation.Duration
        member x.TotalDuration = x.Animation.TotalDuration
        member x.DistanceTime(localTime) = x.Animation.DistanceTime(localTime)
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Commit(lens, name, model) = x.Commit(lens, name, model)
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Value = x.StateMachine.Holder.Value
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'Value>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'Value>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'Value>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'Value>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'Value>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'Value>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'Value>


type private Path<'Model, 'Value> =
    {
        StateMachine : StateMachine<'Value>
        Members : IAnimation<'Model, 'Value>[]
        DistanceTimeFunction : DistanceTimeFunction
        Observable : Observable<'Model, 'Value>
    }

    member x.Offsets =
        (LocalTime.zero, x.Members) ||> Array.scan (fun t a -> t + a.TotalDuration)

    member x.TimeSegments =
        Array.init (x.Offsets.Length - 1) (fun i ->
            GroupSemantics.Segment.create x.Offsets.[i] x.Offsets.[i + 1]
        )

    member x.Value = x.StateMachine.Holder.Value

    member x.State = x.StateMachine.Holder.State

    member x.Duration =
        Array.last x.Offsets |> Duration.ofLocalTime

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.Bidirectional =
        x.DistanceTimeFunction.Bidirectional

    member private x.FindMemberIndex(groupLocalTime : LocalTime) =
        if groupLocalTime < LocalTime.zero then
            0
        elif groupLocalTime > LocalTime.max x.Duration then
            x.Members.Length - 1
        else
            x.TimeSegments |> Array.binarySearch (fun s ->
                if groupLocalTime < s.Start then -1
                elif groupLocalTime > s.End then 1
                else 0
            ) |> ValueOption.get

    member x.DistanceTime(groupLocalTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(groupLocalTime / x.Duration)

    member x.Perform(action : Action) =
        let action = x |> GroupSemantics.applyDistanceTime action

        let perform (i : int) (a : IAnimation<'Model, 'Value>) =
            let action = a |> GroupSemantics.perform x.TimeSegments.[i] x.Bidirectional x.Duration x.State action
            a.Perform(action)

        { x with
            Members = x.Members |> Array.mapi perform
            StateMachine = x.StateMachine |> StateMachine.enqueue action }

    member x.Scale(duration) =
        let s = duration / x.Duration

        let scale (a : IAnimation<'Model, 'Value>) =
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
            let arr : IAnimation<'Model, 'Value>[] = Array.zeroCreate x.Members.Length

            let model =
                (model, x.Members) ||> Array.foldi (fun i model animation ->
                    let model = animation.Commit(lens, name, model)
                    let animation = model |> Optic.get lens
                    arr.[i] <- unbox animation
                    model
                )

            arr, model

        // Process all actions, from oldest to newest
        let machine, events =
            let eval t = let i = x.FindMemberIndex(t) in members.[i].Value
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
module AnimationPathExtensions =

    module Animation =

        /// <summary>
        /// Creates a sequential animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let path (animations : #IAnimation<'Model, 'Value> seq) =
            let animations =
                animations |> Seq.map (fun a -> a :> IAnimation<'Model, 'Value>) |> Array.ofSeq

            if animations.Length = 0 then
                raise <| System.ArgumentException("Animation path cannot be empty")

            { StateMachine = StateMachine.initial
              Members = animations
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'Value>


        let private adapter (animation : IAnimation<'Model>) : IAnimation<'Model, 'Value> =
            { StateMachine = StateMachine.initial
              Animation = animation
              Observable = Observable.empty } :> IAnimation<'Model, 'Value>

        /// <summary>
        /// Creates a sequential animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let sequential (animations : IAnimation<'Model> seq) : IAnimation<'Model, unit> =
            animations |> Seq.map adapter |> path