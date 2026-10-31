namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Animation
open Expecto

module ``Primitives Tests`` =
    open Animation.Primitives.Utilities

    module private Spline =

        let private circle t =
            let angle = 2.0 * System.Math.PI * t
            V3d(cos angle, sin angle, 0.0)

        // Three equal parameter intervals, but edge lengths 3, 4 and 5.
        let private triangle t =
            if t <= 1.0 / 3.0 then V3d(9.0 * t, 0.0, 0.0)
            elif t <= 2.0 / 3.0 then V3d(3.0, 12.0 * t - 4.0, 0.0)
            else V3d(9.0 - 9.0 * t, 12.0 - 12.0 * t, 0.0)

        let private endpoint = Epsilon * 0.5

        // Reach x=2 at t=1/4, then return almost to the start at t=1.
        let private excursion t =
            let x = if t <= 0.25 then 8.0 * t else 2.0 + (endpoint - 2.0) * ((t - 0.25) / 0.75)
            V3d(x, 0.0, 0.0)

        let private closeLength actual expected =
            Expect.isLessThanOrEqual (abs (actual - expected)) 1e-4 "Analytical arc length"

        let private checkQuarters (spline : Splines.Spline<V3d>) expected =
            for i, point in Array.indexed expected do
                let distance = float i / 4.0
                Expect.isLessThanOrEqual (Vec.distance (spline.Evaluate distance) point) 1e-4 $"Position at normalized distance {distance}"

        let private closedCurves = [|
            circle, 2.0 * System.Math.PI,
                [| V3d.XAxis; V3d.YAxis; -V3d.XAxis; -V3d.YAxis; V3d.XAxis |]
            triangle, 12.0,
                [| V3d.Zero; V3d(3.0, 0.0, 0.0); V3d(3.0, 3.0, 0.0); V3d(1.8, 2.4, 0.0); V3d.Zero |]
            excursion, 4.0 - endpoint,
                [| V3d.Zero; V3d(1.0 - endpoint / 4.0, 0.0, 0.0); V3d(2.0 - endpoint / 2.0, 0.0, 0.0); V3d(1.0 + 3.0 * endpoint / 4.0, 0.0, 0.0); V3d(endpoint, 0.0, 0.0) |]
        |]

        let closedLengths =
            test "Closed and nearly closed curves retain interior length" {
                for evaluate, length, _ in closedCurves do
                    Expect.isLessThan (Vec.distance (evaluate 0.0) (evaluate 1.0)) Epsilon "Endpoint chord is below epsilon"
                    let spline = Splines.Spline(Vec.distance, evaluate, Splines.MinErrorTolerance)
                    closeLength spline.Length length
            }

        let closedDistances =
            test "Closed and nearly closed curves use normalized distance" {
                for evaluate, _, expected in closedCurves do
                    let spline = Splines.Spline(Vec.distance, evaluate, Splines.MinErrorTolerance)
                    checkQuarters spline expected
            }

        let constant =
            test "Constant curve terminates without subdivision" {
                let point = V3d(1.0, 2.0, 3.0)
                let mutable evaluations = 0
                let evaluate _ = evaluations <- evaluations + 1; point
                let spline = Splines.Spline(Vec.distance, evaluate, Splines.MinErrorTolerance)
                Expect.isLessThanOrEqual evaluations 3 "At most endpoints and midpoint are examined"
                Expect.equal spline.Length 0.0 "Constant length"
                Expect.equal spline.Samples [| 0.0; 1.0 |] "No subdivision"
                checkQuarters spline (Array.create 5 point)
            }

        let openCurves =
            test "Open curves retain analytical length and parameterization" {
                let line t = V3d(3.0 * t, 4.0 * t, 0.0)
                let straight = Splines.Spline(Vec.distance, line, Splines.MinErrorTolerance)
                closeLength straight.Length 5.0
                Expect.equal straight.Samples [| 0.0; 0.5; 1.0 |] "Existing forced initial subdivision"
                for t in [| -0.25; 0.0; 0.25; 0.5; 0.75; 1.0; 1.25 |] do
                    Expect.v3dClose Accuracy.high (straight.Evaluate t) (V3d(3.0 * t, 4.0 * t, 0.0)) "Linear distance, including extrapolation"

                let arc = Splines.Spline(Vec.distance, (fun t -> circle (t / 4.0)), Splines.MinErrorTolerance)
                closeLength arc.Length (System.Math.PI / 2.0)
                for t in [| 0.0; 0.25; 0.5; 0.75; 1.0 |] do
                    let angle = System.Math.PI * t / 2.0
                    Expect.isLessThanOrEqual (Vec.distance (arc.Evaluate t) (V3d(cos angle, sin angle, 0.0))) 1e-4 "Open arc position"
            }

        let private sampleCases = [|
            "constant", (fun _ -> V3d(1.0, 2.0, 3.0)), Splines.MinErrorTolerance, [| 0.0; 1.0 |]
            "straight", (fun t -> V3d(3.0 * t, 4.0 * t, 0.0)), Splines.MinErrorTolerance, [| 0.0; 0.5; 1.0 |]
            // A circle's chord-error ratio is sec(pi / (2*n)) for n equal segments.
            "default circle", circle, Splines.DefaultErrorTolerance, Array.init 17 (fun i -> float i / 16.0)
            "dense circle", circle, Splines.MinErrorTolerance, Array.init 513 (fun i -> float i / 512.0)
            "parabola", (fun t -> V3d(t, t * t, 0.0)), Splines.DefaultErrorTolerance, [| 0.0; 0.25; 0.5; 1.0 |]
        |]

        let sampleSnapshots =
            test "Samples retain exact boundaries and independent mutable snapshots" {
                for name, curve, tolerance, expected in sampleCases do
                    let mutable curveCalls, distanceCalls = 0, 0
                    let evaluate t = curveCalls <- curveCalls + 1; curve t
                    let distance a b = distanceCalls <- distanceCalls + 1; Vec.distance a b
                    let spline = Splines.Spline(distance, evaluate, tolerance)
                    let length = spline.Length
                    let positions = [| 0.0; 0.125; 0.5; 0.875; 1.0 |]
                    let evaluated = positions |> Array.map spline.Evaluate
                    let calls = curveCalls, distanceCalls
                    let first = spline.Samples
                    let second = spline.Samples
                    let bits values = values |> Array.map BitConverter.DoubleToInt64Bits
                    Expect.equal (bits first) (bits expected) $"{name}: analytical parameter boundaries, bit for bit"
                    Expect.isFalse (obj.ReferenceEquals(first, second)) $"{name}: every snapshot is fresh"
                    Array.fill first 0 first.Length nan
                    Expect.equal second expected $"{name}: mutating one snapshot cannot change another"
                    let third = spline.Samples
                    Expect.isFalse (obj.ReferenceEquals(second, third)) $"{name}: later snapshots remain distinct"
                    Expect.equal (bits third) (bits expected) $"{name}: mutation cannot change later boundaries"
                    Expect.equal (curveCalls, distanceCalls) calls $"{name}: Samples invokes neither delegate"
                    Expect.equal spline.Length length $"{name}: snapshots do not change arc length"
                    Expect.equal (positions |> Array.map spline.Evaluate) evaluated $"{name}: mutation cannot change evaluation"
            }

        let sampleAllocations =
            test "Subdivided Samples allocate only the returned float array" {
                let reads = 256
                let measure (read: unit -> float[]) =
                    for _ = 1 to 32 do GC.KeepAlive(read())
                    let mutable checksum = 0.0
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    for i = 0 to reads - 1 do
                        let samples = read()
                        checksum <- checksum + samples.[i % samples.Length]
                    let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                    allocated, checksum

                let results = [|
                    for name, curve, tolerance, expected in sampleCases do
                        if expected.Length > 3 then
                            let spline = Splines.Spline(Vec.distance, curve, tolerance)
                            let arrayBytes, _ = measure (fun () -> Array.zeroCreate<float> expected.Length)
                            let sampleBytes, checksum = measure (fun () -> spline.Samples)
                            let expectedChecksum =
                                [| 0 .. reads - 1 |] |> Array.sumBy (fun i -> expected.[i % expected.Length])
                            yield name, arrayBytes, sampleBytes, checksum, expectedChecksum
                |]
                for name, arrayBytes, sampleBytes, checksum, expectedChecksum in results do
                    Expect.equal checksum expectedChecksum $"{name}: consume actual snapshot values"
                    Expect.isLessThanOrEqual sampleBytes (arrayBytes + int64 reads * 128L)
                        $"{name}: snapshot allocation {sampleBytes / int64 reads} B/read exceeds float-array allocation {arrayBytes / int64 reads} B/read plus 128 B"
            }

        let infiniteTolerance =
            test "Infinite tolerance bypasses midpoint examination" {
                let mutable evaluations = 0
                let evaluate t = evaluations <- evaluations + 1; triangle t
                let spline = Splines.Spline(Vec.distance, evaluate, infinity)
                Expect.equal evaluations 2 "Only endpoints are examined"
                Expect.equal spline.Length 0.0 "Endpoint chord is retained when subdivision is disabled"
                Expect.equal spline.Samples [| 0.0; 1.0 |] "No subdivision"
                Expect.v3dClose Accuracy.high (spline.Evaluate 0.25) (V3d(2.25, 0.0, 0.0)) "Unsubdivided evaluation uses the original parameter"
            }

        let nonFinite =
            test "Non-finite distances retain their existing fallback" {
                let mutable evaluations = 0
                let evaluate t = evaluations <- evaluations + 1; triangle t
                let spline = Splines.Spline((fun _ _ -> nan), evaluate, Splines.MinErrorTolerance)
                Expect.equal evaluations 2 "NaN endpoint length bypasses subdivision"
                Expect.isTrue (isNaN spline.Length) "NaN length is retained"
                Expect.equal spline.Samples [| 0.0; 1.0 |] "No subdivision"
                for invalid in [| nan; infinity |] do
                    let distance (a : V3d) b = if a = b then 0.0 else invalid
                    let spline = Splines.Spline(distance, triangle, Splines.MinErrorTolerance)
                    Expect.equal spline.Length 0.0 "Undefined interior distances do not replace the original degenerate chord"
                    Expect.equal spline.Samples [| 0.0; 1.0 |] "Invalid interior distances do not cause endless subdivision"
            }

    module private CatmullRom =

        let empty =
            test "Empty" {
                let s = Splines.catmullRom Vec.distance 0.1 Array.empty<V3d>
                Expect.isEmpty s "Invalid spline segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let s = Splines.catmullRom Vec.distance 0.1 [| p |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    Expect.equal r p $"Invalid result for t = {t}"
            }

        let pair =
            test "Pair" {
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(5, 6, 7)
                let s = Splines.catmullRom Vec.distance 0.1 [| p0; p1 |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    let e = lerp p0 p1 t
                    Expect.v3dClose Accuracy.high r e $"Invalid result for t = {t}"
            }

        let pairClose =
            test "Pair Close" {
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + Epsilon, p0.YZ)
                let s = Splines.catmullRom Vec.distance 0.1 [| p0; p1 |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    let e = lerp p0 p1 t
                    Expect.v3dClose Accuracy.high r e $"Invalid result for t = {t}"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let pts = [| p0; p1; p2; p3 |]
                let s = Splines.catmullRom Vec.distance 0.1 pts
                Expect.hasLength s 3 "Invalid spline segment count"

                let n = 32
                for j = 0 to 2 do
                    for i = 0 to n - 1 do
                        let t = float i / float (n - 1)
                        let r = s.[j].Evaluate t
                        let e = lerp pts.[j] pts.[j + 1] t
                        Expect.v3dClose Accuracy.high r e $"Invalid result in segment {j} for t = {t} (j = {i})"
            }

    module private LinearPath =
        
        let empty =
            test "Empty" {
                let a = Animation.Primitives.linearPath' Vec.distance Array.empty<V3d>
                Expect.isEmpty a "Unexpected segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let a = Animation.Primitives.linearPath' Vec.distance [| p |]
                Expect.hasLength a 1 "Invalid segment count"

                let d = a.[0].Duration
                Expect.isTrue d.IsFinite "Infinite duration"
                Expect.equal d.TotalSeconds 0.0 "Invalid duration"
            }

        let simple =
            test "Simple" {
                let a = Animation.Primitives.linearPath Vec.distance [ V3d(1, 2, 3); V3d(-2, 5, -7); V3d(21, -6, 0); ]

                Expect.isTrue a.Duration.IsFinite "Infinite duration"
                Expect.floatClose Accuracy.high a.Duration.TotalSeconds 1.0 "Invalid duration"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let a = Animation.Primitives.linearPath Vec.distance [| p0; p1; p2; p3 |]

                Expect.isTrue a.Duration.IsFinite "Infinite duration"
                Expect.equal a.Duration.TotalSeconds 0.0 "Invalid duration"
            }

    module private SmoothPath =

        let empty =
            test "Empty" {
                let a = Animation.Primitives.smoothPath' Vec.distance 0.1 Array.empty<V3d>
                Expect.isEmpty a "Unexpected segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let a = Animation.Primitives.smoothPath' Vec.distance 0.1 [| p |]
                Expect.hasLength a 1 "Invalid segment count"

                let d = a.[0].Duration
                Expect.isTrue d.IsFinite "Infinite duration"
                Expect.equal d.TotalSeconds 0.0 "Invalid duration"
            }

        let simple =
            test "Simple" {
                let a = Animation.Primitives.smoothPath Vec.distance 0.1 [ V3d(1, 2, 3); V3d(-2, 5, -7); V3d(21, -6, 0); ]

                Expect.isTrue a.Duration.IsFinite "Infinite duration"
                Expect.floatClose Accuracy.high a.Duration.TotalSeconds 1.0 "Invalid duration"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let a = Animation.Primitives.smoothPath Vec.distance 0.1 [| p0; p1; p2; p3 |]

                Expect.isTrue a.Duration.IsFinite "Infinite duration"
                Expect.equal a.Duration.TotalSeconds 0.0 "Invalid duration"
            }

    module Camera =

        let coincidingLocations =
            test "Coinciding Locations" {
                use _ = Animator.initTest()

                let eps = Epsilon * Epsilon * 0.5
                let c0 = CameraView.look V3d.Zero V3d.XAxis V3d.ZAxis
                let c1 = CameraView.look (V3d(eps, 0.0, 0.0)) V3d.YAxis V3d.ZAxis
                let c2 = CameraView.look (-V3d(eps, 0.0, 0.0)) (V3d(1.0, 2.0, 3.0).Normalized) V3d.ZAxis
                let c3 = CameraView.look V3d.Zero (V3d(1.0, 1.0, 1.0).Normalized) V3d.ZAxis

                let a =
                    Animation.Camera.smoothPath 0.1 [| c0; c1; c2; c3 |]

                Expect.isTrue a.Duration.IsFinite "Infinite duration"
                Expect.floatClose Accuracy.high a.Duration.TotalSeconds 0.0 "Invalid duration"
            }

    [<Tests>]
    let tests =
        testList "Animation.Primitives" [
            testList "Spline" [
                Spline.closedLengths
                Spline.closedDistances
                Spline.constant
                Spline.openCurves
                Spline.sampleSnapshots
                Spline.sampleAllocations
                Spline.infiniteTolerance
                Spline.nonFinite
            ]

            testList "Catmull-Rom" [
                CatmullRom.empty
                CatmullRom.single
                CatmullRom.pair
                CatmullRom.pairClose
                CatmullRom.degenerated
            ]

            testList "Linear Path" [
                LinearPath.empty
                LinearPath.single
                LinearPath.simple
                LinearPath.degenerated
            ]

            testList "Smooth Path" [
                SmoothPath.empty
                SmoothPath.single
                SmoothPath.simple
                SmoothPath.degenerated
            ]

            testList "Camera" [
                Camera.coincidingLocations
            ]
        ]