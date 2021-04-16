﻿namespace Aardvark.UI.Anewmation

open System.Collections.Generic

[<Struct>]
type private StateHolder<'Value> =
    {
        mutable State : State
        mutable Value : 'Value
    }

type private StateMachine<'Value> =
    class
        val mutable Holder : StateHolder<'Value>
        val Actions : List<Action>

        new () = {
            Holder = { State = State.Stopped; Value = Unchecked.defaultof<_>};
            Actions = List()
        }
    end

[<Struct>]
type private EventTrigger<'Value> =
    {
        Type : EventType
        Value : 'Value
    }

type private EventQueue<'Value> = ArrayQueue<EventTrigger<'Value>>


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateHolder =

    let processAction (evaluate : LocalTime -> 'Value) (action : Action) (queue : EventQueue<'Value>) (holder : StateHolder<'Value> byref)  =
        match action with
        | Action.Stop ->
            match holder.State with
            | State.Stopped -> ()
            | _ ->
                holder.State <- State.Stopped
                holder.Value <- evaluate LocalTime.zero
                queue.Enqueue { Type = EventType.Stop; Value = holder.Value }

        | Action.Start (globalTime, startFrom) ->
            holder.Value <- evaluate startFrom
            holder.State <- State.Running (globalTime - startFrom)
            queue.Enqueue { Type = EventType.Start; Value = holder.Value }
            queue.Enqueue { Type = EventType.Progress; Value = holder.Value }

        | Action.Pause globalTime ->
            match holder.State with
            | State.Running startTime ->
                holder.State <- State.Paused (startTime, globalTime)
                queue.Enqueue { Type = EventType.Pause; Value = holder.Value }
            | _ -> ()

        | Action.Resume globalTime ->
            match holder.State with
            | State.Paused (startTime, pauseTime) ->
                holder.State <- State.Running (globalTime - (pauseTime - startTime))
                queue.Enqueue { Type = EventType.Resume; Value = holder.Value }
            | _ -> ()

        | Action.Update (globalTime, finalize) ->
            match holder.State with
            | State.Running startTime ->
                holder.Value <- globalTime |> LocalTime.relative startTime |> evaluate
                queue.Enqueue { Type = EventType.Progress; Value = holder.Value }

                if finalize then
                    holder.State <- State.Finished
                    queue.Enqueue { Type = EventType.Finalize; Value = holder.Value }
            | _ ->
                ()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateMachine =

    let enqueue (action : Action) (machine : StateMachine<'Value>) =
        machine.Actions.Add(action)

    let run (evaluate : LocalTime -> 'Value) (queue : EventQueue<'Value>) (machine : StateMachine<'Value>) =
        for action in machine.Actions do
            StateHolder.processAction evaluate action queue &machine.Holder

        machine.Actions.Clear()