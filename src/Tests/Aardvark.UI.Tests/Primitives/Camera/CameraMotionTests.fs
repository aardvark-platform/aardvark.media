namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.FreeFlyController
open Expecto

module ``CameraMotion Tests`` =

    // Branchless original formula: do not share the optimized matrix selection.
    let private original (l : CameraMotion) (r : CameraMotion) =
        let matrix =
            M44d.Rotation(V3d(1.0, 0.0, 0.0), l.dRot.X) *
            M44d.Rotation(V3d(0.0, 1.0, 0.0), l.dRot.Y) *
            M44d.Rotation(V3d(0.0, 0.0, 1.0), l.dRot.Z)
        { dWorldPos = l.dWorldPos + r.dWorldPos
          dPos = l.dPos + matrix.TransformDir r.dPos
          dRot = l.dRot + r.dRot
          dWorldForward = l.dWorldForward + r.dWorldForward
          dWorldUp = l.dWorldUp + r.dWorldUp
          dMoveSpeed = l.dMoveSpeed + r.dMoveSpeed
          dZoom = l.dZoom + r.dZoom
          dPan = l.dPan + r.dPan
          dDolly = l.dDolly + r.dDolly }

    let private components (m : CameraMotion) = [|
        m.dWorldPos.X; m.dWorldPos.Y; m.dWorldPos.Z
        m.dPos.X; m.dPos.Y; m.dPos.Z
        m.dRot.X; m.dRot.Y; m.dRot.Z
        m.dWorldForward.X; m.dWorldForward.Y; m.dWorldForward.Z
        m.dWorldUp.X; m.dWorldUp.Y; m.dWorldUp.Z
        m.dMoveSpeed; m.dZoom; m.dPan.X; m.dPan.Y; m.dDolly
    |]

    let private bits values = values |> Array.map BitConverter.DoubleToInt64Bits
    let private checkMotion context left right =
        // Even the original formula can select either operand's NaN payload
        // under JIT compilation. Require NaN classification, all other bits exact.
        let comparable values =
            values |> Array.map (fun v -> if Double.IsNaN v then 0x7ff8000000000000L else BitConverter.DoubleToInt64Bits v)
        Expect.equal (components (left + right) |> comparable) (components (original left right) |> comparable)
            $"{context}: all twenty values, signed-zero/infinity bits and NaN classification"

    let private negativeZero = BitConverter.Int64BitsToDouble Int64.MinValue
    let private values = [|
        0.0; negativeZero; Double.Epsilon; -Double.Epsilon; 1e-300; -1e-300
        0.125; -2.5; 7.0; 1e150; -1e150; Double.MaxValue; Double.MinValue
        infinity; -infinity
        BitConverter.Int64BitsToDouble 0x7ff8000000001234L
        BitConverter.Int64BitsToDouble (Int64.MinValue ||| 0x7ff8000000004321L)
    |]
    let private motions = [|
        for seed = 0 to values.Length - 1 do
            let at i = values.[(seed + i * 7) % values.Length]
            yield
                { dWorldPos = V3d(at 0, at 1, at 2); dPos = V3d(at 3, at 4, at 5)
                  dRot = V3d(at 6, at 7, at 8); dWorldForward = V3d(at 9, at 10, at 11)
                  dWorldUp = V3d(at 12, at 13, at 14); dMoveSpeed = at 15
                  dZoom = at 16; dPan = V2d(at 17, at 18); dDolly = at 19 }
    |]

    let private checkRotations rotations =
        for index, rotation in Array.indexed rotations do
            for i, left in Array.indexed motions do
                for j, right in Array.indexed motions do
                    checkMotion $"rotation {index}, inputs {i}/{j}" { left with dRot = rotation } right

    let private tinyRotations = [|
        for axis in [V3d.XAxis; V3d.YAxis; V3d.ZAxis] do
            for angle in [Double.Epsilon; -Double.Epsilon; 2.0 * Double.Epsilon; -2.0 * Double.Epsilon; 1e-300; -1e-300; 1e-12; -1e-12; 1e-9; -1e-9] do
                yield axis, angle * axis
    |]

    // Only translation is active, so every derivative has the same motion.
    // Use the original composition at every sum, including the zero contributions.
    let private translationStep (state : CameraControllerState) h =
        let move = { CameraMotion.Zero with dPos = state.moveVec * exp state.freeFlyConfig.moveSensitivity }
        let velocity = [1 .. 5] |> List.fold (fun m _ -> original m CameraMotion.Zero) move
        let k = h * velocity
        let sum = original (original (original k (2.0 * k)) (2.0 * k)) k
        state + sum * (1.0 / 6.0)

    let private renderedReference now (state : CameraControllerState) =
        let rec integrate m dt =
            if dt <= 0.0166666 then translationStep m dt
            else integrate (translationStep m 0.0166666) (dt - 0.0166666)
        let m = integrate state (now - state.lastTime.Value)
        let view = if state.animating then m.view.WithLocation m.view.Location else m.view
        { m with view = view; lastTime = Some now }

    let private viewComponents (view : CameraView) = [|
        for v in [view.Location; view.Sky; view.Forward; view.Up; view.Right] do
            yield v.X; yield v.Y; yield v.Z
    |]

    [<Tests>]
    let tests = testList "Primitives.Camera.Controller.FreeFly.Motion" [
        test "Exact zero rotations retain all component values and signed zeros" {
            let rotations = [|
                for mask = 0 to 7 do
                    let zero bit = if mask &&& bit = 0 then 0.0 else negativeZero
                    yield V3d(zero 1, zero 2, zero 4)
            |]
            for rotation in rotations do Expect.equal rotation V3d.Zero "Signed-zero variants compare exactly equal"
            checkRotations rotations
            let signed = { CameraMotion.Zero with dPos = V3d(negativeZero, 0.0, 0.0) }
            Expect.equal (BitConverter.DoubleToInt64Bits((original signed signed).dPos.X)) 0L "Identity TransformDir arithmetic changes the negative-zero sum"
            checkMotion "Do not bypass identity transformation arithmetic" signed signed
            let exceptional = { CameraMotion.Zero with dPos = V3d(infinity, 1.0, 2.0) }
            let propagated = original CameraMotion.Zero exceptional
            Expect.isTrue (Double.IsPositiveInfinity propagated.dPos.X && Double.IsNaN propagated.dPos.Y && Double.IsNaN propagated.dPos.Z)
                "Zero matrix coefficients still multiply infinity"
            checkMotion "Preserve nonfinite identity transformation" CameraMotion.Zero exceptional
        }
        test "Tiny nonzero rotations retain observable legacy transformation" {
            checkRotations (tinyRotations |> Array.map snd)
            for axis, rotation in tinyRotations do
                let probe =
                    if axis = V3d.XAxis then V3d(0.0, 1e300, 0.0)
                    elif axis = V3d.YAxis then V3d(0.0, 0.0, 1e300)
                    else V3d(1e300, 0.0, 0.0)
                let left = { CameraMotion.Zero with dRot = rotation }
                let right = { CameraMotion.Zero with dPos = probe }
                let transformed = original left right
                let angle = max (abs rotation.X) (max (abs rotation.Y) (abs rotation.Z))
                let actual = bits [| transformed.dPos.X; transformed.dPos.Y; transformed.dPos.Z |]
                let identity = bits [| probe.X; probe.Y; probe.Z |]
                if angle > Double.Epsilon then
                    Expect.notEqual actual identity "The oracle rejects an approximate-zero shortcut, including resolvable subnormal rotations"
                else
                    // The original quaternion constructor halves this angle to zero.
                    Expect.equal actual identity "Retain the minimum-subnormal half-angle underflow"
                checkMotion "Observable tiny cross-component" left right
        }
        test "Ordinary mixed and nonfinite rotations retain original composition" {
            checkRotations [|
                V3d(0.25, 0.0, 0.0); V3d(0.0, -0.5, 0.0); V3d(0.0, 0.0, 1.0)
                V3d(0.3, -0.7, 1.1); V3d(-Math.PI, 0.125, Math.PI)
                V3d(infinity, 0.0, 0.0); V3d(0.0, -infinity, 0.0)
                V3d(0.0, 0.0, values.[15]); V3d(values.[16], 0.25, -0.5)
            |]
        }
        test "Complete translation-only Rendered updates retain frame and state" {
            lock FreeFlyController.sw (fun () ->
                let running = FreeFlyController.sw.IsRunning
                FreeFlyController.sw.Stop()
                try
                    let now = FreeFlyController.sw.Elapsed.TotalSeconds
                    let cameras = [
                        FreeFlyController.initial.view
                        CameraView(V3d(0.0, 0.0, 3.0), V3d(2.0, -3.0, 5.0), V3d.XAxis, V3d.ZAxis, -V3d.YAxis)
                        CameraView.lookAt (V3d(-4.0, 2.0, 7.0)) V3d.Zero V3d.YAxis
                    ]
                    for camera in cameras do
                        for move in [V3d.XAxis; -V3d.YAxis; V3d.ZAxis; V3d(0.3, -0.2, 0.7)] do
                            for dt in [0.0; 0.01; 0.02; 0.05] do
                                for animating in [false; true] do
                                    let before = { FreeFlyController.initial with view = camera; moveVec = move; animating = animating; lastTime = Some(now - dt) }
                                    let expected = renderedReference now before
                                    let actual = FreeFlyController.update before FreeFlyController.Rendered
                                    Expect.equal (viewComponents actual.view |> bits) (viewComponents expected.view |> bits) "Original RK4 composition produces the identical complete camera frame"
                                    Expect.equal { actual with view = expected.view } expected "All remaining controller state is retained"
                finally
                    if running then FreeFlyController.sw.Start())
        }
    ]
