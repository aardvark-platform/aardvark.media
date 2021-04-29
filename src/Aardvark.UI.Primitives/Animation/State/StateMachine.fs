namespace Aardvark.UI.Anewmation

open System.Collections.Generic

[<Struct>]
type private EventTrigger<'Value> =
    {
        Type : EventType
        Value : 'Value
    }

type private EventQueue<'Value> = ArrayQueue<EventTrigger<'Value>>

type private StateMachine<'Value> =
    class
        val mutable State : State
        val mutable Value : 'Value
        val mutable Position : LocalTime
        val Actions : List<Action>

        new () = {
            State = State.Stopped;
            Value = Unchecked.defaultof<_>;
            Position = LocalTime.zero
            Actions = List()
        }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateMachine =

    let private processAction (evaluate : LocalTime -> 'Value) (action : Action) (tick : GlobalTime) (queue : EventQueue<'Value>) (machine : StateMachine<'Value>)  =
        match action with
        | Action.Stop ->
            match machine.State with
            | State.Stopped -> ()
            | _ ->
                machine.State <- State.Stopped
                machine.Value <- evaluate LocalTime.zero
                machine.Position <- LocalTime.zero
                queue.Enqueue { Type = EventType.Stop; Value = machine.Value }

        | Action.Start startFrom ->
            machine.Value <- evaluate startFrom
            machine.State <- State.Running (tick - startFrom)
            machine.Position <- startFrom
            queue.Enqueue { Type = EventType.Start; Value = machine.Value }
            queue.Enqueue { Type = EventType.Progress; Value = machine.Value }

        | Action.Pause ->
            match machine.State with
            | State.Running startTime ->
                machine.State <- State.Paused (startTime, tick)
                queue.Enqueue { Type = EventType.Pause; Value = machine.Value }
            | _ -> ()

        | Action.Resume ->
            match machine.State with
            | State.Paused (startTime, pauseTime) ->
                machine.State <- State.Running (tick - (pauseTime - startTime))
                queue.Enqueue { Type = EventType.Resume; Value = machine.Value }
            | _ -> ()

        | Action.Update (time, finalize) ->
            match machine.State with
            | State.Running _ ->
                machine.Value <- evaluate time
                machine.Position <- time
                queue.Enqueue { Type = EventType.Progress; Value = machine.Value }

                if finalize then
                    machine.State <- State.Finished
                    queue.Enqueue { Type = EventType.Finalize; Value = machine.Value }
            | _ ->
                ()

    let enqueue (action : Action) (machine : StateMachine<'Value>) =
        machine.Actions.Add(action)

    let run (evaluate : LocalTime -> 'Value) (tick : GlobalTime) (queue : EventQueue<'Value>) (machine : StateMachine<'Value>) =
        for action in machine.Actions do
            processAction evaluate action tick queue machine

        machine.Actions.Clear()