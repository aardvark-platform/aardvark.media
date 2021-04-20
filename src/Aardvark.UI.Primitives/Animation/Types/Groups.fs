namespace Aardvark.UI.Anewmation

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Groups =

    [<Struct>]
    type Segment =
        { Start : LocalTime; End : LocalTime }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Segment =

        let create (s : LocalTime) (e : LocalTime) =
            { Start = s; End = e}

        let ofDuration (d : Duration) =
            { Start = LocalTime.zero; End = LocalTime.max d}


    let applyDistanceTime (action : Action) (animation : IAnimationInstance<'Model>) =

        let apply (localTime : LocalTime) =
            let d = animation.Duration
            if d.IsFinite then
                LocalTime.max (d * animation.DistanceTime(localTime))
            else
                localTime

        match action with
        | Action.Start (globalTime, startFrom) ->
            Action.Start (globalTime, apply startFrom)

        | Action.Update (globalTime, finalize) ->
            match animation.State with
            | State.Running startTime ->
                let localTime = globalTime |> LocalTime.relative startTime
                Action.Update (startTime + apply localTime, finalize)

            | _ ->
                action

        | _ ->
            action

    let perform (segment : Segment) (bidirectional : bool) (action : Action) (group : IAnimationInstance<'Model>) (animation : IAnimationInstance<'Model>) =

        let outOfBounds t =
            (t < segment.Start && segment.Start <> LocalTime.zero) ||
            (t > segment.End && segment.End <> LocalTime.max group.Duration)

        let endTime groupLocalTime globalTime =
            let endLocalTime =
                if groupLocalTime < segment.Start && bidirectional then
                    segment.Start
                else
                    segment.End

            globalTime + (endLocalTime - groupLocalTime)

        // Relay actions to members, starting them if necessary
        let start (globalTime : GlobalTime) (groupLocalTime : LocalTime) =
            if outOfBounds groupLocalTime then
                Action.Stop
            else
                Action.Start (globalTime, groupLocalTime - segment.Start)

        let update (finalize : bool) (globalTime : GlobalTime) (groupLocalTime : LocalTime) (animation : IAnimationInstance<'Model>) =
            if outOfBounds groupLocalTime then
                Action.Update (globalTime |> endTime groupLocalTime, true)
            else
                if animation.IsRunning then
                    Action.Update (globalTime, finalize)
                else
                    Action.Start (globalTime, groupLocalTime - segment.Start)

        let action =
            match action, group.State with
            | Action.Start (globalTime, groupLocalTime), _ ->
                start globalTime groupLocalTime

            | Action.Update (globalTime, finalize), State.Running groupStartTime ->
                let groupLocalTime = globalTime |> LocalTime.relative groupStartTime
                update finalize globalTime groupLocalTime animation

            | _ ->
                action

        animation.Perform(action)