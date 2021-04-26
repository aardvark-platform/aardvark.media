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


    /// Applies the distance-time function of the animation to time stamps contained in the given action.
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
            Action.Update (apply time, finalize)

        | _ ->
            action

    /// Performs a group action for the given member.
    let perform (segment : Segment) (action : Action) (group : IAnimationInstance<'Model>) (animation : IAnimationInstance<'Model>) =
        let isFirstSegment = (segment.Start = LocalTime.zero)
        let isLastSegment = (segment.End = LocalTime.max group.Duration)

        let inBounds t =
            (t >= segment.Start || isFirstSegment) && (t < segment.End || isLastSegment)

        let localBoundary start =
            if start then LocalTime.zero else segment.End - segment.Start

        // Start the member if inbounds, stop if out of bounds
        let start groupLocalTime =
            if inBounds groupLocalTime then
                animation.Perform <| Action.Start (groupLocalTime - segment.Start)
            else
                animation.Perform Action.Stop

        // Update the member if inbounds (start if necessary), finalize otherwise
        let update groupLocalTime finalize =
            if inBounds groupLocalTime then
                let localTime = groupLocalTime - segment.Start

                if animation.IsRunning then
                    animation.Perform <| Action.Update (localTime, finalize)
                else
                    let startTime = localBoundary (group.Position < groupLocalTime)
                    animation.Perform <| Action.Start startTime

                    if startTime <> localTime then
                        animation.Perform <| Action.Update (localTime, false)
            else
                let endTime = localBoundary (group.Position > groupLocalTime)
                animation.Perform <| Action.Update (endTime, true)

        match action with
        | Action.Start groupLocalTime ->
            start groupLocalTime

        | Action.Update (groupLocalTime, finalize) when group.IsRunning ->
            update groupLocalTime finalize

        | _ ->
            ()