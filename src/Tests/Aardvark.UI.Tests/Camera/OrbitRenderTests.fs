namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.UI.Primitives
open Expecto

module ``Orbit Render Tests`` =

    let private withFrozenClock action =
        lock OrbitController.sw (fun () ->
            let running = OrbitController.sw.IsRunning
            OrbitController.sw.Stop()
            try action OrbitController.sw.MicroTime
            finally if running then OrbitController.sw.Start())

    let private bases = [
        V3d.YAxis, V3d.ZAxis
        V3d.XAxis, V3d.YAxis
        V3d(1.0, -1.0, 0.0) / sqrt 2.0, V3d(1.0, 1.0, 2.0) / sqrt 6.0
        2.0 * V3d.XAxis, 3.0 * V3d.YAxis
    ]
    let private active mask bit = mask &&& bit <> 0
    let private prepare basis mask tiny last speed =
        let right, sky = basis
        let m = OrbitState.create' right sky (V3d(2.0, -3.0, 4.0)) 0.7 0.2 5.0
        let d = if tiny then 1e-5 else 1.0
        { m with pan = V2d(0.25, -0.5)
                 targetPan = V2d(0.25, -0.5) + (if active mask 1 then V2d(0.4 * d, -0.3 * d) else V2d.Zero)
                 targetPhi = m.phi + (if active mask 2 then -0.4 * d else 0.0)
                 targetTheta = m.theta + (if active mask 4 then 0.3 * d else 0.0)
                 targetRadius = m._radius + (if active mask 8 then 2.0 * d else 0.0)
                 targetCenter = m.center + (if active mask 16 then V3d(d, -2.0 * d, 3.0 * d) else V3d.Zero)
                 lastRender = last; speed = speed; dragStart = Some(V2i(17, 31)); panning = true
                 moveSensitivity = 2.5; zoomSensitivity = -0.4
                 radiusRange = Range1d(0.3, 80.0); thetaRange = Range1d(-1.1, 1.1)
                 config = { isPan = fun button -> button = MouseButtons.Right } }

    let private cross (a: V3d) (b: V3d) =
        V3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X)
    let private norm (a: V3d) = a / Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z)
    let private near (actual: V3d) (expected: V3d) label =
        Expect.isLessThanOrEqual (actual - expected).Length 1e-10 label

    let private check (before: OrbitState) (actual: OrbitState) =
        let dt = before.lastRender |> Option.map (fun last -> (actual.lastRender.Value - last).TotalSeconds) |> Option.defaultValue 0.0
        let part = if dt > 0.0 then Math.Max(0.0, Math.Min(1.0, before.speed * dt / 0.05)) else 0.0
        let scalar start target =
            let delta = target - start
            if abs delta < 1e-4 then target else start + part * delta
        let dp = before.targetPan - before.pan
        let pan = if Math.Sqrt(dp.X * dp.X + dp.Y * dp.Y) < 1e-4 then before.targetPan else before.pan + part * dp
        let dc = before.targetCenter - before.center
        let center =
            if abs dc.X <= 1e-4 && abs dc.Y <= 1e-4 && abs dc.Z <= 1e-4 then before.targetCenter
            else before.center + part * dc
        let phi, theta, radius = scalar before.phi before.targetPhi, scalar before.theta before.targetTheta, scalar before._radius before.targetRadius
        Expect.equal (actual.pan, actual.phi, actual.theta, actual._radius, actual.center) (pan, phi, theta, radius, center) "Independent coordinate interpolation and snapping"
        Expect.equal (actual.sky, actual.right, actual.targetPan, actual.targetPhi, actual.targetTheta, actual.targetRadius, actual.targetCenter)
            (before.sky, before.right, before.targetPan, before.targetPhi, before.targetTheta, before.targetRadius, before.targetCenter) "Raw basis and targets survive"
        Expect.equal (actual.dragStart, actual.panning, actual.radiusRange, actual.thetaRange, actual.moveSensitivity, actual.zoomSensitivity, actual.speed)
            (before.dragStart, before.panning, before.radiusRange, before.thetaRange, before.moveSensitivity, before.zoomSensitivity, before.speed) "Interaction state and configuration values survive"
        Expect.isTrue (obj.ReferenceEquals(actual.config, before.config)) "Configuration identity survives"
        if dp = V2d.Zero && dc = V3d.Zero && before.phi = before.targetPhi && before.theta = before.targetTheta && before._radius = before.targetRadius then
            Expect.isTrue (obj.ReferenceEquals(actual._view, before._view)) "No delta retains the exact camera, without reconstruction"
        else
            Expect.isFalse (obj.ReferenceEquals(actual._view, before._view)) "A pending component reconstructs even if progress is zero"
            // Scalar spherical coordinates in the supplied basis, not OrbitState.withView.
            let position = center + before.right * (radius * Math.Cos phi * Math.Cos theta) +
                           cross before.sky before.right * (radius * Math.Sin phi * Math.Cos theta) + before.sky * (radius * Math.Sin theta)
            let forward = norm (center - position)
            let right = norm (cross forward before.sky)
            let up = norm (cross right forward)
            near actual._view.Location (position + pan.X * right + pan.Y * up) "Independent final panned camera location"
            near actual._view.Forward forward "Independent final forward"
            near actual._view.Right right "Independent final right"
            near actual._view.Up up "Independent final up"
            Expect.equal actual._view.Sky before.sky "View retains raw Sky"
        part

    // Keep measurement outside Expecto computation expressions; setup and assertions are excluded.
    let private allocated (model: OrbitState) =
        for _ in 1 .. 256 do GC.KeepAlive(OrbitController.update model Rendered)
        let mutable minimum = Int64.MaxValue
        let mutable checksum = 0.0
        for _ in 1 .. 3 do
            let before = GC.GetAllocatedBytesForCurrentThread()
            for _ in 1 .. 128 do
                let result = OrbitController.update model Rendered
                checksum <- checksum + result._view.Location.X
            minimum <- min minimum (GC.GetAllocatedBytesForCurrentThread() - before)
        float minimum / 128.0, checksum

    [<Tests>]
    let tests =
        testList "Orbit.Rendered" [
            test "All component subsets preserve interpolation camera geometry and state" {
                withFrozenClock (fun now ->
                    for basis in bases do
                        for tiny in [false; true] do
                            for name, last, speed in [
                                "initial", None, 1.0
                                "full", Some(now - MicroTime.FromSeconds 1.0), 1.0
                                "fraction", Some(now - MicroTime.FromSeconds 0.025), 0.8
                                "fraction", Some(now - MicroTime.FromSeconds 0.04), 0.4
                                "zero speed", Some(now - MicroTime.FromSeconds 1.0), 0.0
                                "zero elapsed", Some now, 1.0
                                "negative elapsed", Some(now + MicroTime.FromSeconds 1.0), 1.0
                                "negative speed", Some(now - MicroTime.FromSeconds 1.0), -1.0
                            ] do
                                for mask in 0 .. 31 do
                                    let before = prepare basis mask tiny last speed
                                    let actual = OrbitController.update before Rendered
                                    Expect.equal actual.lastRender (Some now) "Exactly the sampled clock is recorded"
                                    let part = check before actual
                                    if name = "fraction" then
                                        Expect.isGreaterThan part 0.0 "Fractional case advances"
                                        Expect.isLessThan part 1.0 "Fractional case does not complete"
                                    elif name = "full" then Expect.equal part 1.0 "Full case completes"
                                    else Expect.equal part 0.0 "Initial or disabled progress remains zero")
            }

            test "Scalar and pan tiny predicates differ from componentwise center snapping" {
                withFrozenClock (fun now ->
                    for value in [-1.001e-4; -1e-4; -0.999e-4; 0.0; 0.999e-4; 1e-4; 1.001e-4] do
                        for elapsed in [0.0; 0.025; 1.0] do
                            let m = prepare bases.Head 0 false (Some(now - MicroTime.FromSeconds elapsed)) 1.0
                            let m = { m with phi = 0.0; theta = 0.0; pan = V2d.Zero; center = V3d.Zero
                                             targetPhi = value; targetTheta = -value; targetRadius = m._radius + value
                                             targetPan = V2d(value, value); targetCenter = V3d(value, -value, value) }
                            let actual = OrbitController.update m Rendered
                            check m actual |> ignore
                            if abs value = 1e-4 && elapsed = 0.0 then
                                Expect.equal actual.phi 0.0 "Scalar equality with epsilon is not tiny"
                                Expect.equal actual.pan V2d.Zero "Pan uses Euclidean length, not componentwise tolerance"
                                Expect.equal actual.center m.targetCenter "Center includes the epsilon boundary per component")
            }

            test "Inactive NaN target deltas retain their coordinates and reconstruction eligibility" {
                withFrozenClock (fun now ->
                    let m = prepare bases.Head 0 false (Some(now - MicroTime.Second)) 1.0
                    let m = { m with targetPan = V2d(Double.NaN); targetPhi = Double.NaN; targetTheta = Double.NaN
                                     targetRadius = Double.NaN; targetCenter = V3d(Double.NaN) }
                    let idle = OrbitController.update m Rendered
                    Expect.isTrue (obj.ReferenceEquals(idle._view, m._view)) "NaN deltas do not satisfy the existing positive-delta predicates"
                    let actual = OrbitController.update { m with targetRadius = m._radius + 1.0 } Rendered
                    Expect.equal (actual.pan, actual.phi, actual.theta, actual.center) (m.pan, m.phi, m.theta, m.center) "Inactive components are not interpolated just because another component changes"
                    Expect.equal actual._radius (m._radius + 1.0) "The independent radius update still completes"
                    Expect.isTrue actual._view.Location.IsFinite "Ignored NaN targets do not enter the final geometry"
                    Expect.equal actual.lastRender (Some now) "Time bookkeeping remains active")
            }

            test "Multiple pending components stay within one camera reconstruction budget" {
                withFrozenClock (fun now ->
                    let measurements = [
                        for tiny in [false; true] do
                            for last in [None; Some(now - MicroTime.FromSeconds 0.025)] do
                                for mask in 1 .. 31 do
                                    if mask &&& (mask - 1) <> 0 then
                                        let single = prepare bases.Head (mask &&& -mask) tiny last 1.0 |> allocated
                                        let multiple = prepare bases.Head mask tiny last 1.0 |> allocated
                                        yield mask, tiny, last, single, multiple
                    ]
                    for mask, tiny, last, (single, a), (multiple, b) in measurements do
                        Expect.isTrue (Double.IsFinite a && Double.IsFinite b) "Consume every completed update"
                        Expect.isLessThanOrEqual multiple (single + 512.0) $"mask={mask}, tiny={tiny}, last={last}: no per-component camera reconstruction")
            }
        ]
