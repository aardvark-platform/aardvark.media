namespace Aardvark.UI.Animation

open Aardvark.Base
open OptimizedClosures

type internal ConcurrentGroupInstance<'Model, 'Value>(name : Symbol, definition : ConcurrentGroup<'Model, 'Value>) =
    inherit AbstractAnimationInstance<'Model, 'Value, ConcurrentGroup<'Model, 'Value>>(name, definition)

    let members = definition.Members.Data |> Array.map (fun a -> a.Create name)
    let segments = definition.Members.Data |> Array.map (fun a -> Groups.Segment.ofDuration a.Duration)

    override x.Perform(action) =
        let action = Groups.applyDistanceTimeToAction action x

        for i = 0 to members.Length - 1 do
            members.[i] |> Groups.perform segments.[i] action x

        StateMachine.enqueue action x.StateMachine

    override x.Commit(model, tick) =

        // Commit members
        let mutable result =
            (model, members) ||> Array.fold (fun model animation ->
                animation.Commit(model, tick)
            )

        // Process all actions, from oldest to newest
        let evaluate _ = definition.Mapping.Invoke(result, members)
        StateMachine.run evaluate tick x.EventQueue x.StateMachine

        // Notify observers about changes
        Observable.notify x.Definition.Observable x.Name x.EventQueue &result

        result

and internal ConcurrentGroupMembers<'Model>(members : IAnimation<'Model>[]) =
    let duration =
        ValueCache (fun _ ->
            members |> Array.map _.TotalDuration |> Array.max
        )

    member x.Data : IAnimation<'Model>[] = members
    member x.GroupDuration : Duration = duration.Value

and internal ConcurrentGroup<'Model, 'Value> =
    {
        Members              : ConcurrentGroupMembers<'Model>
        Mapping              : FSharpFunc<'Model, IAnimationInstance<'Model>[], 'Value>
        DistanceTimeFunction : DistanceTimeFunction
        Observable           : Observable<'Model, 'Value>
    }

    member x.Create(name) =
        ConcurrentGroupInstance(name, x)

    member x.Duration =
        x.Members.GroupDuration

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.DistanceTime(groupLocalTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(groupLocalTime / x.Duration)

    member x.Scale(duration) =
        let s = x |> Groups.scale duration

        let scale (a : IAnimation<'Model>) =
            a.Scale(a.Duration * s)

        { x with Members = ConcurrentGroupMembers (x.Members.Data |> Array.map scale) }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    member x.Subscribe(event : EventType, callback : Symbol -> 'Value -> 'Model -> 'Model) =
        { x with Observable = x.Observable |> Observable.subscribe event callback }

    member x.UnsubscribeAll() =
        { x with
            Members    = ConcurrentGroupMembers (x.Members.Data |> Array.map _.UnsubscribeAll())
            Observable = Observable.empty }

    interface IAnimation with
        member x.Duration = x.Duration
        member x.TotalDuration = x.TotalDuration
        member x.DistanceTime(localTime) = x.DistanceTime(localTime)

    interface IAnimation<'Model> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'Value> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model, 'Value>
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
        let concurrent (animations : IAnimation<'Model> seq) : IAnimation<'Model, unit> =
            let animations =
                animations |> Array.ofSeq

            if animations.Length = 0 then
                raise <| System.ArgumentException("Animation group cannot be empty.")

            { Members              = ConcurrentGroupMembers animations
              Mapping              = FSharpFunc<_,_,_>.Adapt (fun _ -> ignore)
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable           = Observable.empty }

        /// Combines two animations into a concurrent group.
        let andAlso (x : IAnimation<'Model>) (y : IAnimation<'Model>) =
            concurrent [x; y]