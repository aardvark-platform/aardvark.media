namespace Aardvark.UI.Anewmation

open Aardvark.Base
open FSharp.Data.Adaptive
open Aether

type private StateHolder<'Value> =
    {
        State : State
        Value : 'Value
    }

type private StateMachine<'Value> =
    {
        Holder : StateHolder<'Value>
        Actions : Action list
    }

type private EventTrigger<'Value> =
    {
        Type : EventType
        Value : 'Value
    }

type private Observable<'Model, 'Value> =
    {
        Observers : HashMap<IAnimationObserver<'Model>, IAnimationObserver<'Model, 'Value>>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateHolder =

    let initial<'Value> =
        { State = State.Stopped; Value = Unchecked.defaultof<'Value> }

    let processAction (evaluate : LocalTime -> 'Value) (action : Action) (holder : StateHolder<'Value>) =
        match action with
        | Action.Stop ->
            match holder.State with
            | State.Stopped -> holder, []
            | _ ->
                let value = evaluate LocalTime.zero
                let holder = { State = State.Stopped; Value = value }
                holder, [EventType.Stop]

        | Action.Start (globalTime, startFrom) ->
            let value = evaluate startFrom
            let holder = { State = State.Running (globalTime - startFrom); Value = value }
            holder, [EventType.Start; EventType.Progress]

        | Action.Pause globalTime ->
            match holder.State with
            | State.Running startTime ->
                let holder = { holder with State = State.Paused (startTime, globalTime)}
                holder, [EventType.Pause]
            | _ -> holder, []

        | Action.Resume globalTime ->
            match holder.State with
            | State.Paused (startTime, pauseTime) ->
                let holder = { holder with State = State.Running (globalTime - (pauseTime - startTime)) }
                holder, [EventType.Resume]
            | _ -> holder, []

        | Action.Update (globalTime, finalize) ->
            match holder.State with
            | State.Running startTime ->
                let value = globalTime |> LocalTime.relative startTime |> evaluate
                let holder = { holder with Value = value }

                if finalize then
                    { holder with State = State.Finished },
                    [EventType.Progress; EventType.Finalize]
                else
                    holder,
                    [EventType.Progress]
            | _ ->
                holder, []


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateMachine =

    let initial<'Value> =
        { Holder = StateHolder.initial<'Value>
          Actions = [] }

    let private trigger (value : 'Value) (event : EventType) =
        { Type = event; Value = value }

    let enqueue (action : Action) (machine : StateMachine<'Value>) =
        { machine with Actions = action :: machine.Actions }

    let run (evaluate : LocalTime -> 'Value) (machine : StateMachine<'Value>) =
        let holder, events =
            (machine.Actions, (machine.Holder, [])) ||> List.foldBack (fun action (state, triggers) ->
                let updated, events = state |> StateHolder.processAction evaluate action
                let triggered = events |> List.map (trigger updated.Value)
                updated, triggers @ triggered
            )

        { Holder = holder; Actions = [] }, events


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Observable =

    let empty : Observable<'Model, 'Value> =
        { Observers = HashMap.empty }

    let add (key : IAnimationObserver<'Model>) (value : IAnimationObserver<'Model, 'Value>) (observable : Observable<'Model, 'Value>) =
        if not value.IsEmpty then
            { Observers = observable.Observers |> HashMap.add key value }
        else
            observable

    let tryRemove (key : IAnimationObserver<'Model>) (observable : Observable<'Model, 'Value>) =
        match observable.Observers |> HashMap.tryRemove key with
        | Some (removed, result) -> Some (removed, { Observers = result })
        | _ -> None

    let subscribe (observer : IAnimationObserver<'Model, 'Value>) (observable : Observable<'Model, 'Value>) =
        observable |> add (observer :> IAnimationObserver<'Model>) observer

    let unsubscribe (observer : IAnimationObserver<'Model>) (observable : Observable<'Model, 'Value>) =
        { Observers = observable.Observers |> HashMap.remove observer }

    let notify (name : Symbol) (events : EventTrigger<'Value> list) (model : 'Model) (observable : Observable<'Model, 'Value>) =
        let invoke model event =
            (model, observable.Observers |> HashMap.toSeq)
            ||> Seq.fold (fun model (_, obs) -> obs.OnNext(model, name, event.Type, event.Value))

        (model, events) ||> Seq.fold invoke