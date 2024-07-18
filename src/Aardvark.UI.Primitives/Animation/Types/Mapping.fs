namespace Aardvark.UI.Animation

open Aardvark.Base
open OptimizedClosures

type private MappingInstance<'Model, 'T, 'U>(name : Symbol, definition : Mapping<'Model, 'T, 'U>) =
    let input = definition.Input.Create(name)
    let mutable value = System.Func<'U> (fun _ -> Unchecked.defaultof<'U>)

    member x.Name = name
    member x.State = input.State
    member x.Value = value.Invoke()
    member x.Position = input.Position
    member x.Definition = definition

    member x.Perform(action) =
        input.Perform(action)

    member x.Commit(model, tick) =
        let model = input.Commit(model, tick)
        value <- System.Func<_> (fun _ -> definition.Mapping.Invoke(model, input.Value))
        model

    interface IAnimation with
        member x.Duration = definition.Duration
        member x.TotalDuration = definition.TotalDuration
        member x.DistanceTime(localTime) = definition.DistanceTime(localTime)

    interface IAnimationInstance<'Model> with
         member x.Name = x.Name
         member x.State = x.State
         member x.Position = x.Position
         member x.Perform(action) = x.Perform(action)
         member x.Commit(model, tick) = x.Commit(model, tick)
         member x.Definition = x.Definition :> IAnimation<'Model>

    interface IAnimationInstance<'Model, 'U> with
        member x.Value = x.Value
        member x.Definition = x.Definition :> IAnimation<'Model, 'U>


and private Mapping<'Model, 'T, 'U> =
    {
        Input : IAnimation<'Model, 'T>
        Mapping : FSharpFunc<'Model, 'T, 'U>
    }

    member x.Duration =
        x.Input.Duration

    member x.TotalDuration =
        x.Input.TotalDuration

    member x.DistanceTime(localTime) =
        x.Input.DistanceTime(localTime)

    member x.Create(name) =
        MappingInstance(name, x)

    member x.Scale(duration) =
        { x with Input = x.Input.Scale(duration)}

    member x.Ease(easing, compose) =
        { x with Input = x.Input.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with Input = x.Input.Loop(iterations, mode)}

    member x.Subscribe(event : EventType, callback : Symbol -> 'U -> 'Model -> 'Model) =
        let mapped name value model =
            model |> callback name (x.Mapping.Invoke(model, value))

        { x with Input = x.Input.Subscribe(event, mapped) }

    member x.UnsubscribeAll() =
        { x with Input = x.Input.UnsubscribeAll()}

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

    interface IAnimation<'Model, 'U> with
        member x.Create(name) = x.Create(name) :> IAnimationInstance<'Model, 'U>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'U>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'U>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'U>
        member x.Subscribe(event, callback) = x.Subscribe(event, callback) :> IAnimation<'Model, 'U>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'U>


[<AutoOpen>]
module AnimationMappingExtensions =

    module Animation =

        /// Returns a new animation that applies the mapping function to the input animation.
        let map' (mapping : 'Model -> 'T -> 'U) (animation : IAnimation<'Model, 'T>) =
            //{ Value = System.Func<_> (fun _ -> Unchecked.defaultof<'U>)
            { Input = animation
              Mapping = FSharpFunc<_,_,_>.Adapt mapping } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animation.
        let map (mapping : 'T -> 'U) (animation : IAnimation<'Model, 'T>) =
            animation |> map' (fun _ x -> mapping x)

        /// Returns a new animation that applies the mapping function to the input animations.
        let map2' (mapping : 'Model -> 'T1 -> 'T2 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) =

            let eval (model : 'Model) (arr : IAnimationInstance<'Model>[]) =
                let x = unbox<IAnimationInstance<'Model, 'T1>> arr.[0]
                let y = unbox<IAnimationInstance<'Model, 'T2>> arr.[1]
                mapping model x.Value y.Value

            { Members = ConcurrentGroupMembers [| x; y |]
              Mapping = FSharpFunc<_,_,_>.Adapt eval
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animations.
        let map2 (mapping : 'T1 -> 'T2 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) =
            (x, y) ||> map2' (fun _ a b -> mapping a b)

        /// Returns a new animation that applies the mapping function to the input animations.
        let map3' (mapping : 'Model -> 'T1 -> 'T2 -> 'T3 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) (z : IAnimation<'Model, 'T3>) =
            let eval (model : 'Model) (arr : IAnimationInstance<'Model>[]) =
                let x = unbox<IAnimationInstance<'Model, 'T1>> arr.[0]
                let y = unbox<IAnimationInstance<'Model, 'T2>> arr.[1]
                let z = unbox<IAnimationInstance<'Model, 'T3>> arr.[2]
                mapping model x.Value y.Value z.Value

            { Members = ConcurrentGroupMembers [| x; y; z |]
              Mapping = FSharpFunc<_,_,_>.Adapt eval
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animations.
        let map3 (mapping : 'T1 -> 'T2 -> 'T3 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) (z : IAnimation<'Model, 'T3>) =
            (x, y, z) |||> map3' (fun _ a b c -> mapping a b c)