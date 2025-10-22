namespace Aardvark.UI.Animation

open Aardvark.Base

type internal SequentialGroupInstance<'Model, 'Value>(name : Symbol, definition : SequentialGroup<'Model, 'Value>) =
    inherit AbstractAnimationInstance<'Model, 'Value, SequentialGroup<'Model, 'Value>>(name, definition)

    let members = definition.Members.Data |> Array.map (fun a -> a.Create name)
    let segments = definition.Members.Segments

    override x.Perform(action) =
        let action = Groups.applyDistanceTime action x

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
        let evaluate t = let i = definition.FindMemberIndex(t) in members.[i].Value
        StateMachine.run evaluate tick x.EventQueue x.StateMachine

        // Notify observers about changes
        Observable.notify x.Definition.Observable x.Name x.EventQueue &result

        result


and internal SequentialGroupMembers<'Model, 'Value>(members : IAnimation<'Model, 'Value>[]) =
    let offsets =
        ValueCache (fun _ ->
            (LocalTime.zero, members) ||> Array.scan (fun t a -> t + a.TotalDuration)
        )

    let segments =
        ValueCache (fun _ ->
            let offsets = offsets.Value
            Array.init (offsets.Length - 1) (fun i ->
                Groups.Segment.create offsets.[i] offsets.[i + 1]
            )
        )

    let duration =
        ValueCache (fun _ ->
            Array.last offsets.Value |> Duration.ofLocalTime
        )

    member x.Data : IAnimation<'Model, 'Value>[] = members
    member x.Segments : Groups.Segment[] = segments.Value
    member x.GroupDuration : Duration = duration.Value


and internal SequentialGroup<'Model, 'Value> =
    {
        Members              : SequentialGroupMembers<'Model, 'Value>
        DistanceTimeFunction : DistanceTimeFunction
        Observable           : Observable<'Model, 'Value>
    }

    member x.Create(name) =
        SequentialGroupInstance(name, x)

    member x.Duration =
        x.Members.GroupDuration

    member x.TotalDuration =
        x.Duration * x.DistanceTimeFunction.Iterations

    member x.FindMemberIndex(groupLocalTime : LocalTime) : int =
        if groupLocalTime < LocalTime.zero then
            0
        elif groupLocalTime > LocalTime.max x.Duration then
            x.Members.Data.Length - 1
        else
            x.Members.Segments |> Array.binarySearch (fun s ->
                if groupLocalTime < s.Start then -1
                elif groupLocalTime > s.End then 1
                else 0
            ) |> ValueOption.get

    member x.DistanceTime(groupLocalTime : LocalTime) =
        x.DistanceTimeFunction.Invoke(groupLocalTime / x.Duration)

    member x.Scale(duration) =
        let s = x |> Groups.scale duration

        let scale (a : IAnimation<'Model, 'Value>) =
            a.Scale(a.Duration * s)

        { x with Members = SequentialGroupMembers (x.Members.Data |> Array.map scale) }

    member x.Ease(easing, compose) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with DistanceTimeFunction = x.DistanceTimeFunction.Loop(iterations, mode)}

    member x.Subscribe(event : EventType, callback : Symbol -> 'Value -> 'Model -> 'Model) =
        { x with Observable = x.Observable |> Observable.subscribe event callback }

    member x.UnsubscribeAll() =
        { x with
            Members    = SequentialGroupMembers (x.Members.Data |> Array.map _.UnsubscribeAll())
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
module AnimationSequentialExtensions =

    module Animation =

        /// <summary>
        /// Creates a sequential animation group from a sequence of animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let sequential (animations : #IAnimation<'Model, 'Value> seq) : IAnimation<'Model, 'Value> =
            let animations =
                animations |> Seq.map (fun a -> a :> IAnimation<'Model, 'Value>) |> Array.ofSeq

            if animations.Length = 0 then
                raise <| System.ArgumentException("Animation group cannot be empty.")

            { Members              = SequentialGroupMembers animations
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable           = Observable.empty }

        /// <summary>
        /// Creates a sequential animation group from a sequence of untyped animations.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
        let sequential' (animations : #IAnimation<'Model> seq) : IAnimation<'Model, unit> =
            animations |> Seq.map Animation.adapter |> sequential