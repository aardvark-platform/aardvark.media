namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Expecto

module ``FreeFly Jump Tests`` =

    let private zero = FreeFlyController.CameraMotion.Zero
    let private sources = [
        for location, elevation, roll in [V3d.Zero, 0.0, 0.0; V3d(3.0, -4.0, 2.0), 0.0, 0.0; V3d(-6.0, 2.0, 4.0), 0.31, -0.27] do
            // Literal orthonormal frame, independent of the controller's camera transformations.
            let forward = V3d(cos elevation, 0.0, sin elevation)
            let vertical = V3d(-sin elevation, 0.0, cos elevation)
            let right = V3d(0.0, -1.0, 0.0)
            yield CameraView(V3d.OOI, location, forward,
                             cos roll * vertical + sin roll * right,
                             cos roll * right - sin roll * vertical)
    ]

    let private pitch (source : CameraView) angle =
        CameraView(source.Sky, source.Location,
                   cos angle * source.Forward + sin angle * source.Up,
                   -sin angle * source.Forward + cos angle * source.Up, source.Right)

    let private yaw (source : CameraView) angle =
        CameraView(source.Sky, source.Location,
                   cos angle * source.Forward + sin angle * source.Right, source.Up,
                   cos angle * source.Right - sin angle * source.Forward)

    let private roll (source : CameraView) angle =
        CameraView(source.Sky, source.Location, source.Forward,
                   cos angle * source.Up + sin angle * source.Right,
                   cos angle * source.Right - sin angle * source.Up)

    let private at location (view : CameraView) =
        CameraView(view.Sky, location, view.Forward, view.Up, view.Right)

    let private vectorClose actual expected context =
        Expect.isTrue ((actual - expected : V3d).Length < 1E-10) context

    let private viewClose (actual : CameraView) (expected : CameraView) =
        for actual, expected in [actual.Location, expected.Location; actual.Forward, expected.Forward;
                                 actual.Up, expected.Up; actual.Right, expected.Right; actual.Sky, expected.Sky] do
            vectorClose actual expected "Camera geometry must match the independent frame"

    let private frame (view : CameraView) =
        for v in [view.Forward; view.Up; view.Right] do
            Expect.isTrue (abs (v.Length - 1.0) < 1E-10) "Unit camera axes"
        Expect.isTrue (abs (Vec.dot view.Forward view.Up) < 1E-10) "Perpendicular forward and up"
        vectorClose (Vec.cross view.Forward view.Up) view.Right "Right-handed camera frame"

    let private state view =
        { FreeFlyController.initial with
            view = view; animating = true; lastTime = Some 12.5
            dragStart = V2i(13, 17); movePos = V2i(19, 23)
            zoom = true; isWheel = true; scrolling = true; forward = true; upward = true
            moveVec = V3d(1.0, 2.0, 3.0); moveSpeed = 1.25; panSpeed = 0.7
            upSpeed = 0.3; downSpeed = 0.2; orbitCenter = Some (V3d(4.0, 5.0, 6.0))
            targetPhiTheta = V2d(0.23, -0.34); targetZoom = 0.04
            targetPan = V2d(0.3, -0.2); targetDolly = 0.6
            freeFlyConfig = { FreeFlyConfig.initial with moveSensitivity = 2.0 }
            stash = Some FreeFlyController.initial }

    let private checkTarget pending (before : CameraControllerState) (target : CameraView) =
        let before = { before with targetJump = Some target }
        let actual = before + zero
        match pending, actual.targetJump with
        | true, Some retained -> Expect.isTrue (obj.ReferenceEquals(retained, target)) "Retain the exact unfinished target"
        | false, None -> ()
        | _ -> failtestf "Expected pending target = %b, got %b" pending actual.targetJump.IsSome
        viewClose actual.view before.view
        Expect.equal { actual with view = before.view; targetJump = before.targetJump } before
            "Completion must not change unrelated controller fields"

    let private unfinished () =
        for source in sources do
            for angle in [-0.4; -0.1; 0.1; 0.4] do
                let target = pitch source angle
                frame target
                Expect.isTrue ((source.Forward - target.Forward).Length > 0.09) "Forward is visibly unfinished"
                Expect.isTrue ((source.Up - target.Up).Length > 0.09) "Up is visibly unfinished"
                checkTarget true (state source) target
            for angle in [-0.4; 0.4] do
                let forwardOnly, upOnly = yaw source angle, roll source angle
                Expect.equal forwardOnly.Up source.Up "Forward-only fixture keeps up exact"
                Expect.equal upOnly.Forward source.Forward "Up-only fixture keeps forward exact"
                for target in [forwardOnly; upOnly] do
                    frame target
                    checkTarget true (state source) target

    let private completed () =
        for source in sources do
            for angle in [0.0; -2E-5; 2E-5] do checkTarget false (state source) (pitch source angle)
            for offset in [-2E-5; 2E-5] do
                checkTarget false (state source) (at (source.Location + V3d(offset, 0.0, 0.0)) source)
            for angle in [-2E-4; 2E-4] do checkTarget true (state source) (pitch source angle)

    let private translation () =
        for source in sources do
            for angle in [-0.4; 0.0; 0.4] do
                let target = pitch source angle |> at (source.Location + V3d(1.0, -2.0, 0.5))
                checkTarget true (state source) target
        // The position comparison remains strictly less-than, including at its exact boundary.
        for offset in [-1E-4; 1E-4] do checkTarget true (state sources.Head) (at (V3d(offset, 0.0, 0.0)) sources.Head)
        let source = sources.Head
        let target = pitch source 0.2 |> at (V3d(1.0, 2.0, 3.0))
        let before = { state source with targetJump = Some target }
        let motion =
            { zero with dWorldPos = target.Location - source.Location
                        dWorldForward = target.Location + target.Forward - source.Location - source.Forward
                        dWorldUp = target.Location + target.Up - source.Location - source.Up }
        let arrived = before + motion
        viewClose arrived.view target
        Expect.isTrue arrived.targetJump.IsSome "Completion is still decided from the pre-motion camera"
        checkTarget false arrived target

    let private manual () =
        let source = sources.Head
        let target = pitch source 0.4 |> at (V3d(1.0, 2.0, 3.0))
        let before = { state source with targetJump = Some target }
        let cancellations = [
            yield { before with pan = true }, source
            yield { before with look = true }, source
            yield { before with dolly = true }, source
            for zoom in [-0.051; 0.051] do yield { before with targetZoom = zoom }, source
            for angle in [-0.01; 0.01] do
                yield { before with rotateVec = V3d(angle, 0.0, 0.0) }, pitch source angle
                yield { before with rotateVec = V3d(0.0, angle, 0.0) }, yaw source (-angle)
                yield { before with rotateVec = V3d(0.0, 0.0, angle) }, source
        ]
        for before, expectedView in cancellations do
            let actual = before + zero
            Expect.isNone actual.targetJump "Existing manual interruption must cancel the jump"
            viewClose actual.view expectedView
            Expect.equal { actual with view = before.view; targetJump = before.targetJump } before "Preserve all other fields"
        // The zoom flag, movement latches/vectors and pending look/pan/dolly values do not cancel.
        for zoom in [-0.05; -0.04; 0.0; 0.04; 0.05] do
            checkTarget true { before with targetZoom = zoom } target

    let private noTarget () =
        for source in sources do
            let before = state source
            let actual = before + zero
            Expect.isNone actual.targetJump "No target remains absent"
            viewClose actual.view source
            Expect.equal { actual with view = before.view } before "No-target state compatibility"
        let before = state sources.[1]
        let motion =
            { zero with dPos = V3d(0.5, 0.25, 2.0); dPan = V2d(0.3, -0.2)
                        dDolly = 0.4; dZoom = 0.01; dMoveSpeed = 0.75
                        dWorldPos = V3d(10.0, 20.0, 30.0); dWorldForward = V3d.III; dWorldUp = -V3d.III }
        let actual = before + motion
        viewClose actual.view (at (V3d(5.41, -4.8, 2.05)) before.view)
        Expect.isNone actual.targetJump "World jump deltas are ignored without a target"
        Expect.equal actual.moveSpeed 2.0 "Ordinary speed change"
        Expect.isTrue (abs (actual.targetZoom - 0.03) < 1E-12) "Ordinary zoom remainder"
        Expect.isTrue (abs (actual.targetDolly - 0.2) < 1E-12) "Ordinary dolly remainder"
        Expect.equal actual.targetPan V2d.Zero "Ordinary pan remainder"
        Expect.equal { actual with view = before.view; moveSpeed = before.moveSpeed; targetZoom = before.targetZoom;
                                  targetDolly = before.targetDolly; targetPan = before.targetPan } before "Other fields stay unchanged"

    let private rendered () =
        // Freeze/restore the existing clock; choose elapsed model time without sleeps or a new clock API.
        lock FreeFlyController.sw (fun () ->
            let wasRunning = FreeFlyController.sw.IsRunning
            FreeFlyController.sw.Stop()
            try
                let now = FreeFlyController.sw.Elapsed.TotalSeconds
                for source in sources do
                    for angle in [-0.4; 0.4] do
                        let target = pitch source angle
                        let before = { FreeFlyController.initial with view = source }
                        let started = FreeFlyController.update before (FreeFlyController.JumpTo target)
                        Expect.isTrue started.animating "JumpTo starts animation"
                        Expect.equal started.lastTime (Some now) "JumpTo initializes the existing clock state"
                        let first = FreeFlyController.update { started with lastTime = Some (now - 0.02) } FreeFlyController.Rendered
                        let second = FreeFlyController.update { first with lastTime = Some (now - 0.02) } FreeFlyController.Rendered
                        for axis in [CameraView.forward; CameraView.up] do
                            let initialError = (axis source - axis target).Length
                            let firstError = (axis first.view - axis target).Length
                            let secondError = (axis second.view - axis target).Length
                            Expect.isTrue (firstError < initialError - 1E-3) "Initial update rotates toward the target"
                            Expect.isTrue (secondError < firstError - 1E-3) "Next Rendered update must continue rotation instead of losing the target"
                            Expect.isTrue (secondError > 0.05) "The target is still genuinely unfinished"
                        for current in [first; second] do
                            frame current.view
                            vectorClose current.view.Location source.Location "Orientation-only jump stays at the destination position"
                            Expect.isTrue (current.targetJump |> Option.exists (fun t -> obj.ReferenceEquals(t, target))) "Retain JumpTo destination"
                            Expect.isTrue current.animating "Continue animating the pending jump"
                            Expect.equal current.lastTime (Some now) "Rendered updates existing time bookkeeping"
            finally
                if wasRunning then FreeFlyController.sw.Start())

    [<Tests>]
    let tests =
        testList "FreeFly.Jump" [
            test "Unfinished pitch and single-axis orientation retain their targets" { unfinished () }
            test "Completed position and orientation clear their targets" { completed () }
            test "Pending translation and pre-motion completion keep their policy" { translation () }
            test "Manual interruptions retain their existing conditions" { manual () }
            test "Absent targets preserve ordinary motion" { noTarget () }
            test "JumpTo Rendered keeps rotating after the initial update" { rendered () }
        ]
