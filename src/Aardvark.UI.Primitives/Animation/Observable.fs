namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open OptimizedClosures

type private Callback<'Model, 'Value> =
    FSharpFunc<Symbol, 'Value, 'Model, 'Model>

type private Observable<'Model, 'Value> =
    HashMap<EventType, Callback<'Model, 'Value>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Observable =

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

    let notify (observable : Observable<'Model, 'Value>) (name : Symbol) (events : EventQueue<'Value> inref) (model : byref<'Model>) =
        let mutable event = Unchecked.defaultof<_>

        while events.Dequeue &event do
            trigger observable name event.Type &event.Value &model

