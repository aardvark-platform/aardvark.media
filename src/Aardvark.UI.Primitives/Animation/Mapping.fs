namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aether

type private MappingObserver<'Model, 'T, 'U> =
    {
        Output : IAnimationObserver<'Model, 'U>
        Mapping : System.Func<'Model, 'T, 'U>
    }

    member x.OnNext(model, name, event, value) =
        x.Output.OnNext(model, name, event, x.Mapping.Invoke(model, value))

    interface IAnimationObserver<'Model, 'T> with
        member x.IsEmpty = x.Output.IsEmpty
        member x.Add(callback, event) = x :> IAnimationObserver<'Model, 'T>
        member x.OnNext(model, name, event, value) = x.OnNext(model, name, event, value)

type private Mapping<'Model, 'T, 'U> =
    {
        Value : System.Func<'U>
        Input : IAnimation<'Model, 'T>
        Mapping : System.Func<'Model, 'T, 'U>
        Observable : Observable<'Model, 'T>
    }

    member x.Perform(action) =
        { x with Input = x.Input.Perform(action) }

    member x.Scale(duration) =
        { x with Input = x.Input.Scale(duration)}

    member x.Ease(easing, compose) =
        { x with Input = x.Input.Ease(easing, compose)}

    member x.Loop(iterations, mode) =
        { x with Input = x.Input.Loop(iterations, mode)}

    member x.Subscribe(observer : IAnimationObserver<'Model, 'U>) =
        let mapped = { Output = observer; Mapping = x.Mapping }
        { x with
            Input = x.Input.Subscribe(mapped)
            Observable = x.Observable |> Observable.add observer mapped }

    member x.UnsubscribeAll() =
        { x with
            Input = x.Input.UnsubscribeAll()
            Observable = Observable.empty }

    member x.Unsubscribe(observer : IAnimationObserver<'Model>) =
        match x.Observable |> Observable.tryRemove observer with
        | Some (mapped, observable) ->
            { x with
                Input = x.Input.Unsubscribe(mapped)
                Observable = observable }
        | _ ->
            x

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
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model>

    interface IAnimation<'Model, 'U> with
        member x.Value = x.Value.Invoke()
        member x.Perform(action) = x.Perform(action) :> IAnimation<'Model, 'U>
        member x.Scale(duration) = x.Scale(duration) :> IAnimation<'Model, 'U>
        member x.Ease(easing, compose) = x.Ease(easing, compose) :> IAnimation<'Model, 'U>
        member x.Loop(iterations, mode) = x.Loop(iterations, mode) :> IAnimation<'Model, 'U>
        member x.Subscribe(observer) = x.Subscribe(observer) :> IAnimation<'Model, 'U>
        member x.Unsubscribe(observer) = x.Unsubscribe(observer) :> IAnimation<'Model, 'U>
        member x.UnsubscribeAll() = x.UnsubscribeAll() :> IAnimation<'Model, 'U>


[<AutoOpen>]
module AnimationMappingExtensions =

    module Animation =

        /// Returns a new animation that applies the mapping function to the input animation.
        let map' (mapping : 'Model -> 'T -> 'U) (animation : IAnimation<'Model, 'T>) =
            { Value = System.Func<_> (fun _ -> Unchecked.defaultof<'U>)
              Input = animation
              Mapping = System.Func<_,_,_> mapping
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

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
              Mapping = System.Func<_,_,_> eval
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
              Mapping = System.Func<_,_,_> eval
              DistanceTimeFunction = DistanceTimeFunction.empty
              Observable = Observable.empty } :> IAnimation<'Model, 'U>

        /// Returns a new animation that applies the mapping function to the input animations.
        let map3 (mapping : 'T1 -> 'T2 -> 'T3 -> 'U) (x : IAnimation<'Model, 'T1>) (y : IAnimation<'Model, 'T2>) (z : IAnimation<'Model, 'T3>) =
            (x, y, z) |||> map3' (fun _ a b c -> mapping a b c)