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
        | Action.Start startFrom ->
            Action.Start (apply startFrom)

        | Action.Update (time, finalize) ->
            match animation.State with
            | State.Running _ ->
                Action.Update (apply time, finalize)

            | _ ->
                action

        | _ ->
            action

    let perform (segment : Segment) (bidirectional : bool) (action : Action) (group : IAnimationInstance<'Model>) (animation : IAnimationInstance<'Model>) =

        let outOfBounds t =
            (t < segment.Start && segment.Start <> LocalTime.zero) ||
            (t > segment.End && segment.End <> LocalTime.max group.Duration)

        let endTime groupLocalTime =
            if groupLocalTime < segment.Start && bidirectional then
                LocalTime.zero
            else
                segment.End - segment.Start

        // Relay actions to members, starting them if necessary
        let start (groupLocalTime : LocalTime) =
            if outOfBounds groupLocalTime then
                Action.Stop
            else
                Action.Start (groupLocalTime - segment.Start)

        let update (finalize : bool) (groupLocalTime : LocalTime) (animation : IAnimationInstance<'Model>) =
            if outOfBounds groupLocalTime then
                Action.Update (endTime groupLocalTime, true)
            else
                let localTime = groupLocalTime - segment.Start

                if animation.IsRunning then
                    Action.Update (localTime, finalize)
                else
                    Action.Start localTime

        let action =
            match action, group.State with
            | Action.Start groupLocalTime, _ ->
                start groupLocalTime

            | Action.Update (groupLocalTime, finalize), State.Running groupStartTime ->
                update finalize groupLocalTime animation

            | _ ->
                action

        animation.Perform(action)