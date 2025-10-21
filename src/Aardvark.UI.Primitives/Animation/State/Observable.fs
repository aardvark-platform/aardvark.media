namespace Aardvark.UI.Animation

open Aardvark.Base
open FSharp.Data.Adaptive
open OptimizedClosures

type internal Callback<'Model, 'Value> =
    FSharpFunc<Symbol, 'Value, 'Model, 'Model>

type internal Observable<'Model, 'Value> =
    HashMap<EventType, Callback<'Model, 'Value>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Observable =

    let empty : Observable<'Model, 'Value> = HashMap.empty

    let subscribe (event : EventType) (callback : Symbol -> 'Value -> 'Model -> 'Model) (observable : Observable<'Model, 'Value>) =
        let update (value : Callback<'Model, 'Value> voption) =
            match value with
            | ValueSome cbs -> Callback.Adapt (fun name value model -> cbs.Invoke(name, value, model) |> callback name value)
            | _ -> Callback.Adapt callback

        observable |> HashMap.updateV event update

    let private trigger (observable : Observable<'Model, 'Value>) (name : Symbol) (event : EventType) (value : inref<'Value>) (model : byref<'Model>) =
        match observable |> HashMap.tryFindV event with
        | ValueSome cb -> model <- cb.Invoke(name, value, model)
        | _ -> ()

    let notify (observable : Observable<'Model, 'Value>) (name : Symbol) (events : EventQueue<'Value>) (model : byref<'Model>) =
        let mutable event = Unchecked.defaultof<_>

        while events.Dequeue &event do
            trigger observable name event.Type &event.Value &model