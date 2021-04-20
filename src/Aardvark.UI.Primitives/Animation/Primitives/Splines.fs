namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<AutoOpen>]
module AnimationSplinePrimitives =

    module Splines =

        type private Segment<'T> =
            {
                mutable MinT   : float
                mutable MaxT   : float
                mutable Start  : 'T
                mutable End    : 'T
                mutable Length : float
            }

        /// Represents a spline segment, parameterized by normalized arc length.
        /// The accuracy of the parameterization depends on the given epsilon, where values closer to zero result in higher accuracy.
        type Spline<'T>(distance : 'T -> 'T -> float, evaluate : float -> 'T, epsilon : float) =

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
                [a; b]

            let subdivide (segment : Segment<'T>) =
                let rec inner (accum : Segment<'T> list) (segments : Segment<'T> list) =
                    match segments with
                    | [] -> accum
                    | s::st ->
                        let halves = half s
                        let quarters = halves |> List.collect half
                        let quarterLength = s.Length * 0.25

                        let isQuarterValid (x : Segment<'T>) =
                            Fun.ApproximateEquals(x.Length / quarterLength, 1.0, epsilon)

                        if (quarters |> List.forall isQuarterValid) then
                            inner (s :: accum) st   // avoid O(n) append, reverse when finished
                        else
                            inner accum (halves @ st)

                if isFinite epsilon then
                    [ segment ] |> inner [] |> List.rev |> Array.ofList
                else
                    [| segment |]

            let segments, length =

                let s =
                    full |> subdivide |> Array.map (fun s ->
                        { MinT = s.MinT; MaxT = s.MaxT
                          Start = 0.0; End = 0.0
                          Length = s.Length }
                    )

                //// Sum and normalize
                let n = s.Length
                let mutable sum = KahanSum.Zero

                for i = 1 to n - 1 do
                    sum <- sum + s.[i - 1].Length
                    s.[i - 1].End <- sum.Value
                    s.[i].Start <- sum.Value

                sum <- sum + s.[n - 1].Length
                s.[n - 1].End <- sum.Value

                for i = 0 to n - 1 do
                    s.[i].Start <- s.[i].Start / sum.Value
                    s.[i].End <- s.[i].End / sum.Value

                s, sum.Value

            let lookup s =
                let i =
                    if s < 0.0 then 0
                    elif s > 1.0 then segments.Length - 1
                    else
                        segments |> Array.binarySearch (fun segment ->
                            if s < segment.Start then -1 elif s > segment.End then 1  else 0
                        ) |> ValueOption.get

                let t = Fun.InvLerp(s, segments.[i].Start, segments.[i].End)
                t |> lerp segments.[i].MinT segments.[i].MaxT

            member x.Length = length
            member x.Evaluate(s) = s |> lookup |> evaluate
            member x.Samples = segments |> Array.mapi (fun i s -> if i = 0 then [| s.MinT; s.MaxT |] else [| s.MaxT |]) |> Array.concat


        let inline private scale (t : float) (x : ^T) =
            t |> lerp zero x

        /// Computes a Catmull-Rom spline from the given points, parameterized by arc length.
        /// The accuracy of the parameterization depends on the given epsilon, where values closer to zero result in higher accuracy.
        let inline catmullRom (distance : ^T -> ^T -> float) (epsilon : float) (points : ^T[]) : Spline< ^T>[] =

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

                Spline(distance, evaluate, epsilon)


            if Array.isEmpty points then
                Array.empty
            else
                let tj = Array.zeroCreate (points.Length + 2)
                let pj = Array.zeroCreate (points.Length + 2)

                pj.[1] <- points.[0]
                tj.[1] <- 0.0

                let mutable n = 2
                let mutable sum = KahanSum.Zero

                for i = 1 to points.Length - 1 do
                    let d = sqrt (distance pj.[n - 1] points.[i])
                    if d.ApproximateEquals 0.0 then
                        Log.warn "[Animation] Ignoring duplicate control point in spline"
                    else
                        pj.[n] <- points.[i]
                        sum <- sum + d
                        tj.[n] <- sum.Value
                        inc &n

                // At this point n is the number of control points + 1, or number of final points
                // minus 1 since we compute and add a point to each end.
                if n = 2 then
                    let zeroDist _ _ = 0.0
                    let constPoint _ = pj.[1]
                    [| Spline(zeroDist, constPoint, infinity) |]
                else
                    pj.[0] <- pj.[1] + (pj.[1] - pj.[2])
                    tj.[0] <- 0.0

                    pj.[n] <- pj.[n - 1] + (pj.[n - 1] - pj.[n - 2])
                    tj.[n] <- tj.[n - 1] + sqrt (distance pj.[n - 1] pj.[n])

                    let d = sqrt (distance pj.[0] pj.[1])
                    for i = 1 to n do
                        tj.[i] <- tj.[i] + d

                    Array.init (n - 2) (segment tj pj)


    module Animation =

        module Primitives =

            /// Creates an array of animations that smoothly interpolate along the path given by the control points.
            /// The animations are scaled according to the distance between the points. Coinciding points are ignored.
            /// The accuracy of the parameterization depends on the given epsilon, where values closer to zero result in higher accuracy.
            let inline smoothPath' (distance : ^Value -> ^Value -> float) (epsilon : float) (points : ^Value seq) : IAnimation<'Model, ^Value>[] =

                let points = Array.ofSeq points
                let spline = points |> Splines.catmullRom distance epsilon
                let maxLength = spline |> Array.map (fun s -> s.Length) |> Array.max

                spline |> Array.map (fun s ->
                    let duration = s.Length / maxLength

                    Animation.create s.Evaluate
                    |> Animation.seconds (if isFinite duration then duration else 1.0)
                )

            /// Creates an animation that smoothly interpolates along the path given by the control points. Coinciding points are ignored.
            /// The accuracy of the parameterization depends on the given epsilon, where values closer to zero result in higher accuracy.
            let inline smoothPath (distance : ^Value -> ^Value -> float) (epsilon : float) (points : ^Value seq) : IAnimation<'Model, ^Value> =
                points |> smoothPath' distance epsilon |> Animation.path