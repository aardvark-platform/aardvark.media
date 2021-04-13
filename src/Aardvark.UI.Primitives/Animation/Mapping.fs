namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether
open OptimizedClosures

type private Mapping<'Model, 'T, 'U> =
    {
        Value : System.Func<'U>
        Input : IAnimation<'Model, 'T>
        Mapping : FSharpFunc<'Model, 'T, 'U>
    }

    member x.Perform(action) =
        { x with Input = x.Input.Perform(action) }

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

    member x.Commit(lens : Lens<'Model, IAnimation<'Model>>, name : Symbol, model : 'Model) =
        let model = x.Input.Commit(lens, name, model)
        let input = model |> Optic.get lens |> unbox<IAnimation<'Model, 'T>>

        model |> Optic.set lens (
            { x with
                Input = input
                Value = System.Func<_> (fun _ -> x.Mapping.Invoke(model, input.Value))
            } :> IAnimation<'Model>
        )

    interface IAnimation<'Model> with
        member x.State = x.Input.State
        member x.Duration = x.Input.Duration
        member x.TotalDuration = x.Input.TotalDuration
        member x.DistanceTime(localTime) = x.Input.DistanceTime(localTime)
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model>
        member x.Commit(lens, name, model) = x.Commit(lens, name, model)
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'U> with
        member x.Value = x.Value.Invoke()
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'U>
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
            { Value = System.Func<_> (fun _ -> Unchecked.defaultof<'U>)
              Input = animation
              Mapping = FSharpFunc<_,_,_>.Adapt mapping } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animation.
        let map (mapping : 'T -> 'U) (animation : IAnimation<'Model, 'T>) =
            animation |> map' (fun _ x -> mapping x)

        /// Returns a new animation that applies the mapping function to the input animations.
        let map2' (mapping : 'Model -> 'T1 -> 'T2 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) =

            let eval (model : 'Model) (arr : IAnimation<'Model>[]) =
                let x = unbox<IAnimation<'Model, 'T1>> arr.[0]
                let y = unbox<IAnimation<'Model, 'T2>> arr.[1]
                mapping model x.Value y.Value

            { StateMachine = StateMachine.initial
              Members = [| x; y |]
              Mapping = FSharpFunc<_,_,_>.Adapt eval
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animations.
        let map2 (mapping : 'T1 -> 'T2 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) =
            (x, y) ||> map2' (fun _ a b -> mapping a b)

        /// Returns a new animation that applies the mapping function to the input animations.
        let map3' (mapping : 'Model -> 'T1 -> 'T2 -> 'T3 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) (z : IAnimation<'Model, 'T3>) =
            let eval (model : 'Model) (arr : IAnimation<'Model>[]) =
                let x = unbox<IAnimation<'Model, 'T1>> arr.[0]
                let y = unbox<IAnimation<'Model, 'T2>> arr.[1]
                let z = unbox<IAnimation<'Model, 'T3>> arr.[2]
                mapping model x.Value y.Value z.Value

            { StateMachine = StateMachine.initial
              Members = [| x; y; z |]
              Mapping = FSharpFunc<_,_,_>.Adapt eval
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animations.
        let map3 (mapping : 'T1 -> 'T2 -> 'T3 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) (z : IAnimation<'Model, 'T3>) =
            (x, y, z) |||> map3' (fun _ a b c -> mapping a b c)