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

        private new (holder) = { Holder = holder; Actions = List() }

        static member Empty =
            StateMachine<'Value>(
                StateHolder<'Value>(State.Stopped, Unchecked.defaultof<_>)
            )
    end

[<Struct>]
type private EventTrigger<'Value> =
    {
        Type : EventType
        Value : 'Value
    }


type private EventQueue<'Value> =
    struct
        val mutable Data : EventTrigger<'Value>[]
        val mutable Count : int
        val mutable Index : int

        private new (data, count, index) =
            { Data = data; Count = count; Index = index }

        static member Empty =
            EventQueue<'Value>(Array.zeroCreate 1, 0, 0)

        member x.Clear() =
            x.Count <- 0
            x.Index <- 0

        member x.Enqueue(event : EventTrigger<'Value>) =
            if x.Count >= x.Data.Length then
                if x.Index = x.Count then
                    x.Clear()
                else
                    System.Array.Resize(&x.Data, x.Data.Length * 2)

            x.Data.[x.Count] <- event
            x.Count <- x.Count + 1

        member x.Dequeue(result : EventTrigger<'Value> outref) =
            if x.Index < x.Count then
                result <- x.Data.[x.Index]
                x.Index <- x.Index + 1
                true
            else
                false
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private StateHolder =

    let processAction (evaluate : LocalTime -> 'Value) (action : Action) (queue : EventQueue<'Value> inref) (holder : StateHolder<'Value> byref)  =
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

    let enqueue (action : Action) (machine : StateMachine<'Value> inref) =
        machine.Actions.Add(action)

    let run (evaluate : LocalTime -> 'Value) (queue : EventQueue<'Value> inref) (machine : StateMachine<'Value> byref) =
        for action in machine.Actions do
            StateHolder.processAction evaluate action &queue &machine.Holder

        machine.Actions.Clear()