namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.FreeFlyController
open Expecto

module ``FreeFly Motion Tests`` =

    let private motions = [
        "zero", CameraMotion.Zero
        "right", { CameraMotion.Zero with dPos = V3d(0.375, 0.0, 0.0) }
        "Sky", { CameraMotion.Zero with dPos = V3d(0.0, -0.625, 0.0) }
        "forward", { CameraMotion.Zero with dPos = V3d(0.0, 0.0, 1.125) }
        "pan right", { CameraMotion.Zero with dPan = V2d(-0.375, 0.0) }
        "pan up", { CameraMotion.Zero with dPan = V2d(0.0, 0.625) }
        "dolly", { CameraMotion.Zero with dDolly = -0.875 }
        "zoom", { CameraMotion.Zero with dZoom = 1.375 }
        "mixed", { CameraMotion.Zero with dPos = V3d(0.375, -0.625, 1.125); dPan = V2d(-0.375, 0.625); dDolly = -0.875; dZoom = 1.375 }
    ]

    let private cameras = [
        // Literal orthonormal bases and scalar roll, independent of controller transforms.
        for sky, forward, up, right in [
            V3d.OOI, V3d.IOO, V3d.OOI, -V3d.OIO
            V3d(0.0, 7.0, 0.0), V3d.OOI, V3d.OIO, -V3d.IOO
            V3d(0.25, 0.0, 0.0), V3d.OIO, V3d.IOO, -V3d.OOI
            V3d(1.0, 2.0, 2.0) / 3.0, V3d(2.0, 1.0, -2.0) / 3.0, V3d(1.0, 2.0, 2.0) / 3.0, V3d(2.0, -2.0, 1.0) / 3.0
            V3d(1.0, 2.0, 2.0) * (7.0 / 3.0), V3d(2.0, 1.0, -2.0) / 3.0, V3d(1.0, 2.0, 2.0) / 3.0, V3d(2.0, -2.0, 1.0) / 3.0
        ] do
            for roll in [0.0; -0.37; 0.51] do
                for location in [V3d.Zero; V3d(3.0, -4.0, 2.0); V3d(1E12, -2E12, 3E12)] do
                    yield CameraView(sky, location, forward,
                                     cos roll * up + sin roll * right,
                                     cos roll * right - sin roll * up)
    ]

    let private coordinates (cam : CameraView) (motion : CameraMotion) =
        // Scalar, ordered additions: in particular, do not combine dolly and zoom.
        let axis get =
            let local = ((get cam.Location + motion.dPos.X * get cam.Right) + motion.dPos.Y * get cam.Sky) + motion.dPos.Z * get cam.Forward
            let pan = (local + motion.dPan.X * get cam.Right) + motion.dPan.Y * get cam.Up
            let dolly = pan + motion.dDolly * get cam.Forward
            dolly + motion.dZoom * get cam.Forward
        V3d(axis (fun v -> v.X), axis (fun v -> v.Y), axis (fun v -> v.Z))

    let private exactVector context (expected : V3d) (actual : V3d) =
        for e, a in [expected.X, actual.X; expected.Y, actual.Y; expected.Z, actual.Z] do
            Expect.equal (BitConverter.DoubleToInt64Bits a) (BitConverter.DoubleToInt64Bits e) context

    let private frame (source : CameraView) (actual : CameraView) =
        for name, expected, actual in ["Forward", source.Forward, actual.Forward; "Up", source.Up, actual.Up;
                                       "Right", source.Right, actual.Right; "Sky", source.Sky, actual.Sky] do
            exactVector (name + " is unchanged, including raw Sky magnitude") expected actual

    // Measure outside Expecto computation expressions, excluding setup and assertions.
    let private allocated (action : unit -> CameraView) =
        for _ in 1 .. 256 do GC.KeepAlive(action())
        let mutable minimum = Int64.MaxValue
        let mutable checksum = 0.0
        for _ in 1 .. 3 do
            let start = GC.GetAllocatedBytesForCurrentThread()
            for _ in 1 .. 128 do
                let result = action()
                checksum <- checksum + result.Location.X + result.Sky.Y
            minimum <- min minimum (GC.GetAllocatedBytesForCurrentThread() - start)
        float minimum / 128.0, checksum

    let private withFrozenClock action =
        lock sw (fun () ->
            let running = sw.IsRunning
            sw.Stop()
            try action sw.Elapsed.TotalSeconds
            finally if running then sw.Start())

    [<Tests>]
    let tests =
        testList "Primitives.Camera.Controller.FreeFly.Motion" [
            test "Translation components retain ordered coordinates and exact frames" {
                for cam in cameras do
                    for name, motion in motions do
                        let actual = cam + motion
                        exactVector (name + ": independent ordered coordinates") (coordinates cam motion) actual.Location
                        frame cam actual
                        Expect.isFalse (obj.ReferenceEquals(cam, actual)) (name + ": retain a fresh camera even for zero motion")
            }

            test "Large-coordinate cancellation retains separate local pan dolly and zoom additions" {
                let large = 9007199254740992.0 // 2^53: adding one before cancellation must round away.
                for sign in [-1.0; 1.0] do
                    let cam = CameraView(V3d.OOI, V3d(sign * large, -sign * large, 0.0), V3d.IOO, V3d.OOI, -V3d.OIO)
                    for motion, expected in [
                        { CameraMotion.Zero with dDolly = -sign * large; dZoom = sign }, V3d(sign, -sign * large, 0.0)
                        { CameraMotion.Zero with dPos = V3d(sign * large, 0.0, -sign * large); dPan = V2d(-sign, 0.0); dDolly = sign * large; dZoom = sign }, V3d(sign * large, -sign * (2.0 * large), 0.0)
                    ] do
                        let actual = cam + motion
                        exactVector "Literal cancellation result pins addition order" expected actual.Location
                        frame cam actual
            }

            test "Translation-only calls stay within a single-camera allocation budget" {
                let measurements = [
                    for cam in [cameras.[1]; cameras.[31]; cameras.[40]] do
                        let baseline = allocated (fun () -> cam.WithLocation cam.Location)
                        for name, motion in motions do
                            let actual = allocated (fun () -> cam + motion)
                            yield name, baseline, actual
                ]
                for name, (baseline, a), (actual, b) in measurements do
                    Expect.isTrue (Double.IsFinite a && Double.IsFinite b) "Consume every measured camera"
                    Expect.isGreaterThan baseline 0.0 "The baseline really constructs a camera"
                    Expect.isLessThanOrEqual actual (baseline + 64.0) (name + ": no discarded intermediate cameras")
            }

            test "Public Rendered movement preserves frame and controller bookkeeping" {
                withFrozenClock (fun now ->
                    for cam in cameras |> List.filter (fun c -> c.Location = V3d.Zero) do
                        let before =
                            { initial with view = cam; dragStart = V2i(13, 17); movePos = V2i(19, 23)
                                           orbitCenter = Some(V3d(4.0, 5.0, 6.0)); stash = Some initial
                                           freeFlyConfig = { FreeFlyConfig.initial with moveSensitivity = 0.0 } }
                            |> fun m -> update m (KeyDown({ shift = false; alt = false; ctrl = false }, Keys.D))
                            |> fun m -> update m (KeyDown({ shift = false; alt = false; ctrl = false }, Keys.W))
                            |> fun m -> update m (PanUp 0.25)
                        Expect.equal before.moveVec (V3d(1.0, 0.25, 1.0)) "Public inputs start the expected movement"
                        for elapsed in [0.0; 0.01; 0.02; 0.05] do
                            let before = { before with lastTime = Some(now - elapsed) }
                            let actual = update before Rendered
                            let dt = now - before.lastTime.Value
                            let expected = coordinates cam { CameraMotion.Zero with dPos = V3d(dt, 0.25 * dt, dt) }
                            Expect.isLessThanOrEqual (actual.view.Location - expected).Length 1E-12 "Constant velocity integrates in the original camera basis"
                            frame cam actual.view
                            Expect.equal actual.lastTime (Some now) "Rendered records the frozen timestamp"
                            Expect.equal { actual with view = before.view; lastTime = before.lastTime } before "All other controller fields retain their values"
                            Expect.isFalse (obj.ReferenceEquals(actual.view, before.view)) "Animating Rendered retains fresh view identity")
            }
        ]
