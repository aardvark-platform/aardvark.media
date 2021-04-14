namespace Aardvark.UI.Anewmation

open System.Collections.Generic

type private StateHolder<'Value> =
    struct
        val mutable State : State
        val mutable Value : 'Value

        new (state, value) = { State = state; Value = value }
    end

type private StateMachine<'Value> =
    struct
        val mutable Holder : StateHolder<'Value>
        val Actions : List<Action>

        new (holder) = { Holder = holder; Actions = List() }
    end

[<Struct>]
type private EventTrigger<'Value> =
    {
        Type : EventType
        Value : 'Value
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateHolder =

    let initial<'Value> =
        StateHolder(State.Stopped, Unchecked.defaultof<'Value>)

    let processAction (evaluate : LocalTime -> 'Value) (action : Action) (triggers : List<EventTrigger<'Value>>) (holder : byref<StateHolder<'Value>>)  =
        match action with
        | Action.Stop ->
            match holder.State with
            | State.Stopped -> ()
            | _ ->
                holder.State <- State.Stopped
                holder.Value <- evaluate LocalTime.zero
                triggers.Add({ Type = EventType.Stop; Value = holder.Value })

        | Action.Start (globalTime, startFrom) ->
            holder.Value <- evaluate startFrom
            holder.State <- State.Running (globalTime - startFrom)
            triggers.Add({ Type = EventType.Start; Value = holder.Value })
            triggers.Add({ Type = EventType.Progress; Value = holder.Value })

        | Action.Pause globalTime ->
            match holder.State with
            | State.Running startTime ->
                holder.State <- State.Paused (startTime, globalTime)
                triggers.Add({ Type = EventType.Pause; Value = holder.Value })
            | _ -> ()

        | Action.Resume globalTime ->
            match holder.State with
            | State.Paused (startTime, pauseTime) ->
                holder.State <- State.Running (globalTime - (pauseTime - startTime))
                triggers.Add({ Type = EventType.Resume; Value = holder.Value })
            | _ -> ()

        | Action.Update (globalTime, finalize) ->
            match holder.State with
            | State.Running startTime ->
                holder.Value <- globalTime |> LocalTime.relative startTime |> evaluate
                triggers.Add({ Type = EventType.Progress; Value = holder.Value })

                if finalize then
                    holder.State <- State.Finished
                    triggers.Add({ Type = EventType.Finalize; Value = holder.Value })
            | _ ->
                ()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateMachine =

    let initial<'Value> =
        StateMachine(StateHolder.initial<'Value>)

    let enqueue (action : Action) (machine : byref<StateMachine<'Value>>) =
        machine.Actions.Add(action)

    let run (evaluate : LocalTime -> 'Value) (machine : byref<StateMachine<'Value>>) =
        let triggers = List()

        for action in machine.Actions do
            StateHolder.processAction evaluate action triggers &machine.Holder

        machine.Actions.Clear()
        triggers