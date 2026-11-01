namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Expecto

module ``ArcBall Blur Tests`` =

    let private settings = [
        1.0, 0.01, 0.01
        -0.75, 0.04, 0.07
        2.0, 0.2, 0.08
        0.25, 0.0, 0.0
        1.5, -0.03, -0.05
    ]
    let private cameras = [
        CameraView(V3d.ZAxis, V3d(3.0, -4.0, 2.0), V3d.YAxis, V3d.ZAxis, V3d.XAxis)
        CameraView(3.0 * V3d.YAxis, V3d(-2.0, 5.0, 1.0), V3d.XAxis, V3d(0.0, 0.8, 0.6), V3d(0.0, -0.6, 0.8))
    ]
    let private state (sensitivity, pan, zoom) view center =
        { ArcBallController.initial with view = view; orbitCenter = center
                                         sensitivity = sensitivity; panFactor = pan; zoomFactor = zoom }
    let private dirty m =
        { m with dragStart = V2i(17, 23); movePos = V2i(-19, 31)
                 look = true; zoom = true; pan = true; dolly = true; isWheel = true; scrolling = true
                 forward = true; backward = true; left = true; right = true; upward = true; downward = true
                 moveVec = V3d(1.0, 2.0, 3.0); rotateVec = V3d(-0.1, 0.2, -0.3)
                 moveSpeed = 1.25; panSpeed = 0.35; upSpeed = 2.0; downSpeed = 3.0
                 lastTime = Some 99.0; animating = true
                 rotationFactor = 0.8; scrollSensitivity = 0.9
                 targetPhiTheta = V2d(0.2, 0.3); targetPan = V2d(0.4, 0.5); targetDolly = 0.6; targetZoom = 0.7
                 targetJump = Some m.view; stash = Some ArcBallController.initial
                 freeFlyConfig = { FreeFlyConfig.initial with moveSensitivity = 4.0; panDamping = 8.0 } }

    let private blur m = ArcBallController.update m ArcBallController.Blur
    let private near (actual: V3d) (expected: V3d) message =
        Expect.isLessThanOrEqual (actual - expected).Length 1e-10 message
    let private basis (actual: CameraView) (expected: CameraView) =
        Expect.equal (actual.Forward, actual.Up, actual.Right, actual.Sky)
            (expected.Forward, expected.Up, expected.Right, expected.Sky) "Translation preserves the exact camera frame and raw Sky"
    let private shift (scale: float) (x: float) (y: float) (z: float) = V3d(scale * x, scale * y, scale * z)
    let private withFrozenClock action =
        lock ArcBallController.sw (fun () ->
            let running = ArcBallController.sw.IsRunning
            ArcBallController.sw.Stop()
            try action ArcBallController.sw.Elapsed.TotalSeconds
            finally if running then ArcBallController.sw.Start())

    [<Tests>]
    let tests =
        testList "ArcBall.Blur" [
            test "Blur retains only navigation settings camera and orbit center" {
                for tuning in settings do
                    for view in cameras do
                        for center in [None; Some (V3d(7.0, -11.0, 13.0))] do
                            let before = state tuning view center |> dirty
                            let expected = state tuning view center
                            let mutable actual = before
                            for _ in 1 .. 3 do
                                actual <- blur actual
                                Expect.isTrue (obj.ReferenceEquals(actual.view, view)) "Blur retains camera identity"
                                Expect.equal actual.orbitCenter center "Present and absent orbit centers survive"
                                Expect.equal actual expected "All transient state and unrelated tuning reset to existing defaults"
                                Expect.isTrue (ArcBallController.threads actual).store.IsEmpty "Blur leaves no keyboard tick command"
                            Expect.isTrue before.look "Blur does not mutate its input state"
            }

            test "Pan after Blur uses retained sensitivity and pan factor" {
                for sensitivity, pan, zoom in settings do
                    for view in cameras do
                        for hasCenter in [false; true] do
                            let center = if hasCenter then Some (view.Location + 128.0 * view.Forward) else None
                            let clean = state (sensitivity, pan, zoom) view center |> dirty |> blur
                            let down = ArcBallController.update clean (ArcBallController.Down(MouseButtons.Middle, V2i(12, -9)))
                            let actual = ArcBallController.update down (ArcBallController.Move(V2i(32, -6)))
                            // Independent scalar displacement: 20 right pixels and 3 down pixels.
                            let scale = pan * Math.Exp sensitivity
                            let step =
                                if hasCenter then
                                    shift scale (20.0 * view.Right.X - 3.0 * view.Up.X)
                                                (20.0 * view.Right.Y - 3.0 * view.Up.Y)
                                                (20.0 * view.Right.Z - 3.0 * view.Up.Z)
                                else V3d.Zero
                            near actual.view.Location (view.Location + step) "Configured pan displacement after Blur"
                            match actual.orbitCenter, center with
                            | Some actual, Some original -> near actual (original + step) "Pan translates the center by the same displacement"
                            | None, None -> ()
                            | _ -> failtest "Pan must preserve whether an orbit center exists"
                            basis actual.view view
                            Expect.equal actual.dragStart (V2i(32, -6)) "Existing drag bookkeeping"
                            let released = ArcBallController.update actual (ArcBallController.Up MouseButtons.Middle)
                            Expect.isFalse released.pan "Pointer release still stops panning"
            }

            test "Zoom after Blur uses retained sensitivity and zoom factor" {
                for sensitivity, pan, zoom in settings do
                    for view in cameras do
                        let center = view.Location + 128.0 * view.Forward
                        for dy in [-3; 5] do
                            let clean = state (sensitivity, pan, zoom) view (Some center) |> dirty |> blur
                            let down = ArcBallController.update clean (ArcBallController.Down(MouseButtons.Right, V2i(12, -9)))
                            let actual = ArcBallController.update down (ArcBallController.Move(V2i(99, -9 + dy)))
                            let scale = -zoom * Math.Exp sensitivity * float dy
                            let step = shift scale view.Forward.X view.Forward.Y view.Forward.Z
                            near actual.view.Location (view.Location + step) "Configured signed zoom displacement after Blur"
                            Expect.equal actual.orbitCenter (Some center) "Zoom retains the orbit center"
                            basis actual.view view
            }

            test "Keyboard motion retains sensitivity and ignores delayed key releases" {
                withFrozenClock (fun now ->
                    for sensitivity, pan, zoom in settings do
                        for view in cameras do
                            let center = view.Location + 128.0 * view.Forward
                            let clean = state (sensitivity, pan, zoom) view (Some center) |> dirty |> blur
                            let released =
                                [Keys.W; Keys.S; Keys.A; Keys.D]
                                |> List.fold (fun m key -> ArcBallController.update m (ArcBallController.KeyUp key)) clean
                            Expect.equal released clean "Old key releases cannot introduce motion or timestamps after Blur"
                            for key, axis, sign, translatesCenter in [Keys.W, view.Forward, 1.0, false; Keys.S, view.Forward, -1.0, false;
                                                                     Keys.A, view.Right, -1.0, true; Keys.D, view.Right, 1.0, true] do
                                let pressed = ArcBallController.update released (ArcBallController.KeyDown key)
                                Expect.equal pressed.lastTime (Some now) "KeyDown uses the frozen controller clock"
                                let last = now - 0.125
                                let actual = ArcBallController.update { pressed with lastTime = Some last } ArcBallController.StepTime
                                let scale = sign * Math.Exp sensitivity * (now - last)
                                let step = shift scale axis.X axis.Y axis.Z
                                near actual.view.Location (view.Location + step) "Configured independent keyboard displacement after Blur"
                                let expectedCenter = if translatesCenter then center + step else center
                                near actual.orbitCenter.Value expectedCenter "Only horizontal keyboard panning translates the center"
                                basis actual.view view
                                Expect.equal actual.lastTime (Some now) "StepTime retains existing time bookkeeping"
                                let ended = ArcBallController.update actual (ArcBallController.KeyUp key)
                                Expect.equal ended.moveVec V3d.Zero "Key release clears the new press once"
                                Expect.isTrue (ArcBallController.threads ended).store.IsEmpty "Released movement no longer requests ticks")
            }

            test "Default idle and wheel reset behavior remains unchanged" {
                withFrozenClock (fun now ->
                    for view in cameras do
                        for center in [None; Some V3d.Zero] do
                            let clean = state settings.Head view center |> dirty |> blur
                            let idle = ArcBallController.update clean ArcBallController.StepTime
                            Expect.isTrue (obj.ReferenceEquals(idle.view, view)) "First idle step retains camera identity even without a center"
                            Expect.equal idle.orbitCenter center "Idle step preserves an absent center"
                            Expect.equal idle.lastTime (Some now) "The first step only initializes elapsed time"
                            let wheel = ArcBallController.update clean (ArcBallController.Wheel(V2d(1.25, -2.75)))
                            Expect.equal wheel { clean with isWheel = true; moveVec = V3d(0.0, 0.0, -20.0); lastTime = Some now }
                                "Wheel retains integer vertical-delta conversion and existing bookkeeping"
                            Expect.equal (blur wheel) clean "Blur still clears wheel state and motion")
            }
        ]
