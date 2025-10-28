namespace Aardvark.UI.Animation

open Aardvark.Base

[<AutoOpen>]
module AnimationSplinePrimitives =
    open Animation.Primitives.Utilities

    module Splines =

        /// Minimum error tolerance for subdividing spline segments.
        [<Literal>]
        let MinErrorTolerance = 1e-5

        /// Default error tolerance for subdividing spline segments.
        [<Literal>]
        let DefaultErrorTolerance = 1e-2

        [<Struct>]
        type private Segment<'T> =
            {
                mutable MinT   : float
                mutable MaxT   : float
                mutable Start  : 'T
                mutable End    : 'T
                mutable Length : float
            }

        /// Represents a spline segment, parameterized by normalized arc length.
        /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
        /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
        type Spline<'T>(distance : 'T -> 'T -> float, evaluate : float -> 'T, errorTolerance : float) =
            let errorTolerance = max errorTolerance MinErrorTolerance

            let full =
                let p0 = evaluate 0.0
                let p1 = evaluate 1.0

                { MinT = 0.0; MaxT = 1.0;
                  Start = p0; End = p1;
                  Length = distance p0 p1 }

            let half (s : Segment<'T>) =
                let t = (s.MinT + s.MaxT) * 0.5
                let p = evaluate t
                let a = { s with MaxT = t; End = p; Length = distance s.Start p }
                let b = { s with MinT = t; Start = p; Length = distance p s.End }
                struct (a, b)

            let subdivide (segment : Segment<'T>) =
                let rec inner (result : Segment<'T> list) (segments : Segment<'T> list) =
                    match segments with
                    | [] ->
                        result |> List.rev |> Array.ofList // Avoid O(n) append, reverse when finished

                    // Do not subdivide if length is below epsilon
                    | s::rest when s.Length < Epsilon ->
                        inner (s :: result) rest

                    | s::rest ->
                        let struct (a, b) = half s

                        // Subdivide if ratio between sum of halves and full segment exceeds tolerance
                        // Also force an initial subdivision
                        let isWithinErrorTolerance() =
                            if result.IsEmpty && rest.IsEmpty then false
                            else
                                let errorRatio = (a.Length + b.Length) / s.Length
                                Fun.ApproximateEquals(errorRatio, 1.0, errorTolerance)

                        if a.Length < Epsilon || b.Length < Epsilon || isWithinErrorTolerance() then
                            inner (s :: result) rest
                        else
                            inner result (a :: b :: rest)

                if isFinite errorTolerance && not <| isNaN segment.Length then
                    [ segment ] |> inner []
                else
                    [| segment |]

            let struct (segments, length) =

                let s =
                    full |> subdivide |> Array.map (fun s ->
                        { MinT = s.MinT; MaxT = s.MaxT
                          Start = 0.0; End = 0.0
                          Length = s.Length }
                    )

                // Sum and normalize
                let n = s.Length
                let mutable sum = KahanSum.Zero

                for i = 1 to n - 1 do
                    sum <- sum + s.[i - 1].Length
                    s.[i - 1].End <- sum.Value
                    s.[i].Start <- sum.Value

                sum <- sum + s.[n - 1].Length
                s.[n - 1].End <- sum.Value

                if sum.Value >= Epsilon then
                    for i = 0 to n - 1 do
                        s.[i].Start <- s.[i].Start / sum.Value
                        s.[i].End <- s.[i].End / sum.Value

                s.[n - 1].End <- 1.0
                s, sum.Value

            let lookup s =
                let i =
                    if s < 0.0 then 0
                    elif s > 1.0 then segments.Length - 1
                    else
                        segments |> Array.binarySearch (fun segment ->
                            if s < segment.Start then -1 elif s > segment.End then 1 else 0
                        ) |> ValueOption.get

                let t = Fun.InvLerp(s, segments.[i].Start, segments.[i].End)
                t |> lerp segments.[i].MinT segments.[i].MaxT

            member x.Length = length
            member x.Evaluate(s) = s |> lookup |> evaluate
            member x.Samples = segments |> Array.mapi (fun i s -> if i = 0 then [| s.MinT; s.MaxT |] else [| s.MaxT |]) |> Array.concat


        let inline private scale (t : float) (x : ^T) =
            t |> lerp zero x

        /// Computes a centripetal Catmull-Rom spline from the given points, parameterized by arc length.
        /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
        /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
        let inline catmullRom (distance : ^T -> ^T -> float) (errorTolerance : float) (points : ^T[]) : Spline< ^T>[] =

            // Evaluation of a single segment (4 control points)
            let segment (tj : float[]) (pj : ^T[]) (index : int) =
                let t0 = tj.[index]
                let t1 = tj.[index + 1]
                let t2 = tj.[index + 2]
                let t3 = tj.[index + 3]

                let evaluate t =
                    let t = t1 + t * (t2 - t1)
                    let a1 = scale ((t1 - t) / (t1 - t0)) pj.[index + 0] + scale ((t - t0) / (t1 - t0)) pj.[index + 1]
                    let a2 = scale ((t2 - t) / (t2 - t1)) pj.[index + 1] + scale ((t - t1) / (t2 - t1)) pj.[index + 2]
                    let a3 = scale ((t3 - t) / (t3 - t2)) pj.[index + 2] + scale ((t - t2) / (t3 - t2)) pj.[index + 3]
                    let b1 = scale ((t2 - t) / (t2 - t0)) a1 + scale ((t - t0) / (t2 - t0)) a2
                    let b2 = scale ((t3 - t) / (t3 - t1)) a2 + scale ((t - t1) / (t3 - t1)) a3
                    scale ((t2 - t) / (t2 - t1)) b1 + scale ((t - t1) / (t2 - t1)) b2

                Spline(distance, evaluate, errorTolerance)

            // Linear evaluation for segments with zero length
            let linearSegment (pj : ^T[]) (index: int) =
                let evaluate =
                    if Unchecked.equals pj.[index] pj.[index + 1] then
                        fun _ -> pj.[index]
                    else
                        lerp pj.[index] pj.[index + 1]

                Spline(distance, evaluate, infinity)

            if Array.isEmpty points then
                Array.empty

            elif points.Length = 1 then
                let constDist _ _ = 0.0
                let constValue _ = points.[0]
                Array.singleton <| Spline(constDist, constValue, infinity)

            else
                let tj = Array.zeroCreate (points.Length + 2)
                let pj = Array.zeroCreate (points.Length + 2)

                // Offset of the first of four control points for each segment
                // Set to -1 if segment has zero length
                let offset = Array.zeroCreate (points.Length - 1)
                offset.[0] <- -1

                pj.[1] <- points.[0]
                tj.[1] <- 0.0

                let mutable n = 2
                let mutable sum = KahanSum.Zero

                for i = 0 to points.Length - 2 do
                    let d = sqrt (distance pj.[n - 1] points.[i + 1]) // alpha = 0.5 -> centripetal
                    if d < Epsilon then
                        offset.[i] <- -1
                    else
                        offset.[i] <- n - 2
                        pj.[n] <- points.[i + 1]
                        sum <- sum + d
                        tj.[n] <- sum.Value
                        inc &n

                // Add a control point at the beginning and end
                if n > 2 then
                    pj.[0] <- pj.[1] + (pj.[1] - pj.[2])
                    tj.[0] <- 0.0

                    pj.[n] <- pj.[n - 1] + (pj.[n - 1] - pj.[n - 2])
                    tj.[n] <- tj.[n - 1] + (tj.[n - 1] - tj.[n - 2])

                    let d = tj.[2] - tj.[1]
                    for i = 1 to n do
                        tj.[i] <- tj.[i] + d

                Array.init (points.Length - 1) (fun i ->
                    if offset.[i] > -1 then
                        segment tj pj offset.[i]
                    else
                        linearSegment points i
                )

    module Animation =

        module Primitives =

            /// Creates an array of animations that smoothly interpolate along the path given by the control points.
            /// The animations are scaled according to the length of the spline segments.
            /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
            /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
            /// Returns an empty array if the input sequence is empty.
            let inline smoothPath' (distance : ^Value -> ^Value -> float) (errorTolerance : float) (points : ^Value seq) : IAnimation<'Model, ^Value>[] =
                let points = Seq.asArray points
                let spline = points |> Splines.catmullRom distance errorTolerance
                let totalLength = spline |> Array.stableSumBy _.Length

                spline |> Array.map (fun s ->
                    let duration =
                        if totalLength < Epsilon || not <| isFinite s.Length then 0.0
                        else s.Length / totalLength

                    Animation.create s.Evaluate
                    |> Animation.seconds duration
                )

            /// <summary>
            /// Creates an animation that smoothly interpolates along the path given by the control points.
            /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
            /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
            /// Returns an empty animation if the input sequence is empty.
            /// </summary>
            let inline smoothPath (distance : ^Value -> ^Value -> float) (errorTolerance : float) (points : ^Value seq) : IAnimation<'Model, ^Value> =
                points |> smoothPath' distance errorTolerance |> Animation.sequential