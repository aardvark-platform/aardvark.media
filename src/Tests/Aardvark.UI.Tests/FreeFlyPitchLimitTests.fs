namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Primitives.FreeFlyController
open Expecto

module ``FreeFly Pitch Limit Tests`` =

    // Independent scalar geometry: do not use the operator's matrix construction or pole guard.
    let private dot (a : V3d) (b : V3d) = a.X * b.X + a.Y * b.Y + a.Z * b.Z
    let private cross (a : V3d) (b : V3d) = V3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X)
    let private unit v = v / sqrt (dot v v)
    let private rotate axis angle v =
        let axis = unit axis
        cos angle * v + sin angle * cross axis v + (1.0 - cos angle) * dot axis v * axis

    let private location = V3d(3.0, -4.0, 2.0)
    let private camera sky forward =
        let right = unit (cross forward sky)
        CameraView(sky, location, forward, unit (cross right forward), right)
    let private near context (expected : V3d) (actual : V3d) =
        Expect.isTrue ((actual - expected).Length <= 2E-12) $"{context}: expected {expected}, got {actual}"

    let private expectedLocation (cam : CameraView) (motion : CameraMotion) =
        cam.Location + motion.dPos.X * cam.Right + motion.dPos.Y * cam.Sky + motion.dPos.Z * cam.Forward +
        motion.dPan.X * cam.Right + motion.dPan.Y * cam.Up + (motion.dDolly + motion.dZoom) * cam.Forward

    let private expectedForward suppressPitch (cam : CameraView) (motion : CameraMotion) =
        cam.Forward
        |> rotate cam.Forward motion.dRot.Z
        |> rotate cam.Sky motion.dRot.Y
        |> rotate cam.Right (if suppressPitch then 0.0 else motion.dRot.X)
        |> unit

    let private check context (cam : CameraView) motion suppressPitch =
        let actual = cam + motion
        let forward = expectedForward suppressPitch cam motion
        let right = unit (cross forward cam.Sky)
        let up = unit (cross right forward)
        near (context + ": forward") forward actual.Forward
        near (context + ": right") right actual.Right
        near (context + ": up") up actual.Up
        near (context + ": location") (expectedLocation cam motion) actual.Location
        Expect.equal actual.Sky cam.Sky (context + ": retain original Sky, including its magnitude")
        for name, axis in ["forward", actual.Forward; "up", actual.Up; "right", actual.Right] do
            Expect.isTrue (abs (dot axis axis - 1.0) < 2E-12) (context + ": unit " + name)
        near (context + ": handedness") actual.Right (cross actual.Forward actual.Up)
        actual

    let private pitch angle = { CameraMotion.Zero with dRot = V3d(angle, 0.0, 0.0) }
    let private skies = ["X-up", V3d.XAxis; "Y-up", V3d.YAxis; "negative-Y-up", -V3d.YAxis]
    let private rigid = rotate (V3d(1.0, 2.0, -3.0)) 0.71
    let private transform (cam : CameraView) =
        CameraView(rigid cam.Sky, rigid cam.Location, rigid cam.Forward, rigid cam.Up, rigid cam.Right)

    [<Tests>]
    let tests =
        testList "FreeFly.PitchLimit" [
            test "Small pitch facing world Z follows each configured sky" {
                for name, sky in skies do
                    let cam = camera sky V3d.ZAxis
                    for angle in [-0.1; -0.05; 0.05; 0.1] do
                        let actual = check $"{name}, pitch {angle}" cam (pitch angle) false
                        Expect.isTrue ((actual.Forward - cam.Forward).Length > 0.04) "Pitch must actually change the direction"
            }

            test "Sky poles suppress pitch while retaining the yaw and roll fallback" {
                for name, sky in ("default Z-up", V3d.ZAxis) :: (List.tail skies) do
                    let cam = camera sky V3d.XAxis
                    for angle in [-1.5; 1.5] do
                        for yaw, roll in [0.0, 0.0; 0.05, 0.3; -0.05, -0.3] do
                            let motion = { CameraMotion.Zero with dRot = V3d(angle, yaw, roll) }
                            let actual = check $"{name}, pitch {angle}, yaw {yaw}, roll {roll}" cam motion true
                            if yaw <> 0.0 then
                                Expect.isTrue ((actual.Forward - cam.Forward).Length > 0.04) "Suppression must not discard yaw"
            }

            test "Both sides of the existing absolute pole threshold are retained" {
                let limits = [0.99 - 1E-7, false; 0.99 + 1E-7, true]
                for name, sky in ["default Z-up", V3d.ZAxis; "Y-up", V3d.YAxis; "negative-Y-up", -V3d.YAxis] do
                    let cam = camera sky V3d.XAxis
                    for poleDot, suppressed in limits do
                        for sign in [-1.0; 1.0] do
                            let motion = pitch (sign * asin poleDot)
                            let proposed = expectedForward false cam motion
                            Expect.isTrue (abs (abs (dot (unit sky) proposed) - poleDot) < 2E-14) "Fixture must bracket the actual sky pole"
                            check $"{name}, signed pole dot {sign * poleDot}" cam motion suppressed |> ignore
            }

            test "Oblique skies are rotation-consistent and normalized only for the guard" {
                for scale in [0.25; 1.0; 7.0] do
                    let source = camera (scale * V3d.ZAxis) V3d.XAxis
                    let rotated = transform source
                    for angle, suppressed in [-1.5, true; -0.1, false; 0.1, false; 1.5, true] do
                        let motion =
                            { (pitch angle) with
                                dPos = V3d(0.2, -0.3, 0.4); dPan = V2d(-0.1, 0.25)
                                dDolly = 0.15; dZoom = -0.05 }
                        let expected = expectedForward suppressed source motion |> rigid
                        let actual = check $"oblique sky magnitude {scale}, pitch {angle}" rotated motion suppressed
                        near "Rigid coordinate rotation preserves forward" expected actual.Forward
                        near "Rigid coordinate rotation preserves translation" (expectedLocation source motion |> rigid) actual.Location
            }

            test "Default sky and translation-only paths preserve their existing frame" {
                let defaultCam = camera V3d.ZAxis V3d.XAxis
                for angle in [-1.5; -0.1; -0.05; 0.05; 0.1; 1.5] do
                    check $"default Z-up pitch {angle}" defaultCam (pitch angle) (abs angle = 1.5) |> ignore
                for angles in [V3d(0.1, 0.05, 0.3); V3d(-0.1, -0.05, -0.3)] do
                    check "Default combined rotations" defaultCam { CameraMotion.Zero with dRot = angles } false |> ignore
                let cameras = defaultCam :: (skies |> List.map (fun (_, sky) -> camera sky V3d.ZAxis)) @ [transform defaultCam; camera (7.0 * V3d.YAxis) V3d.ZAxis]
                for cam in cameras do
                    for motion in [CameraMotion.Zero; { CameraMotion.Zero with dPos = V3d(0.2, -0.3, 0.4) };
                                   { CameraMotion.Zero with dPan = V2d(-0.1, 0.25); dDolly = 0.15; dZoom = -0.05 }] do
                        let actual = cam + motion
                        near "Translation-only location" (expectedLocation cam motion) actual.Location
                        Expect.equal (actual.Forward, actual.Up, actual.Right, actual.Sky) (cam.Forward, cam.Up, cam.Right, cam.Sky) "Translation retains the exact basis and raw Sky"
                // Preserve the separate world-point reconstruction path, including its existing Z-up convention.
                let delta = V3d(0.2, -0.3, 0.4)
                let motion = { CameraMotion.Zero with dWorldPos = delta; dWorldForward = delta; dWorldUp = delta }
                let actual = defaultCam + motion
                near "World-point translation" (location + delta) actual.Location
                near "World-point forward" defaultCam.Forward actual.Forward
                near "World-point up" defaultCam.Up actual.Up
                Expect.equal actual.Sky V3d.ZAxis "Existing world-point reconstruction convention"
            }
        ]
