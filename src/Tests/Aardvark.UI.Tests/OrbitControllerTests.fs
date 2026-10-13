namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.UI.Primitives
open Expecto

module ``OrbitController Tests`` =

    let private fixtures = [
        true, OrbitState.create (V3d(1.5, -2.0, 3.0)) 0.7 0.4 4.5
        false,
            { OrbitState.create' V3d.IOO V3d.OOI (V3d(-3.0, 1.0, -2.0)) -1.1 -0.35 7.0 with
                thetaRange = Range1d(-0.6, 0.8) }
    ]

    let private requests (model: OrbitState) = [
        0.25, 0.25
        model.thetaRange.Min - 1.0, model.thetaRange.Min
        model.thetaRange.Min, model.thetaRange.Min
        model.thetaRange.Max + 1.0, model.thetaRange.Max
        model.thetaRange.Max, model.thetaRange.Max
    ]

    let private pose (model: OrbitState) =
        model.phi, model.theta, model._radius, model.center, model.pan,
        model._view.Location, model._view.Forward, model._view.Up, model._view.Right, model._view.Sky

    let private checkThetaTarget (before: OrbitState) expected (after: OrbitState) =
        Expect.equal after.targetTheta expected "Clamped elevation target"
        Expect.equal (after.targetPhi, after.targetRadius, after.targetCenter, after.targetPan)
            (before.targetPhi, before.targetRadius, before.targetCenter, before.targetPan) "Unrelated targets"
        Expect.equal (pose after) (pose before) "Current camera pose is deferred until Rendered"
        Expect.equal after.lastRender before.lastRender "Targeting does not advance the render clock"

    let private position defaultBasis (model: OrbitState) phi theta =
        // Independent spherical geometry. The default right=Y basis rotates XY by 90 degrees.
        let x = Math.Cos theta * Math.Cos phi
        let y = Math.Cos theta * Math.Sin phi
        let z = Math.Sin theta
        let direction = if defaultBasis then V3d(-y, x, z) else V3d(x, y, z)
        model.center + model._radius * direction

    let private render defaultBasis (before: OrbitState) phi theta (targeted: OrbitState) =
        // Predates the controller stopwatch origin: interpolation reaches its target without sleeps.
        let rendered = OrbitController.update { targeted with lastRender = Some (MicroTime.FromSeconds -1.0) } Rendered
        let expected = position defaultBasis before phi theta
        Expect.isLessThanOrEqual (rendered._view.Location - expected).Length 1e-10 "Spherical camera position"
        Expect.floatClose Accuracy.high rendered.phi phi "Rendered azimuth"
        Expect.floatClose Accuracy.high rendered.theta theta "Rendered elevation"
        if phi = before.phi then Expect.equal rendered.phi before.phi "Azimuth remains unchanged"
        Expect.equal (rendered.center, rendered._radius, rendered.pan)
            (before.center, before._radius, before.pan) "Unrelated current coordinates"
        Expect.isLessThanOrEqual (rendered._view.Forward - (before.center - expected) / before._radius).Length
            1e-10 "Camera still faces the orbit center"

    let private target (model: OrbitState) request expected =
        let pending =
            { model with
                targetPhi = model.phi + 0.31
                targetRadius = model._radius + 1.7
                targetCenter = model.center + V3d(4.0, 5.0, 6.0)
                targetPan = V2d(0.15, -0.2)
                lastRender = Some (MicroTime.FromSeconds -1.0) }
        OrbitController.update pending (SetTargetTheta request) |> checkThetaTarget pending expected

    let private camera defaultBasis (model: OrbitState) request expected =
        let targeted = OrbitController.update model (SetTargetTheta request)
        Expect.equal (pose targeted) (pose model) "No immediate camera motion"
        render defaultBasis model model.phi expected targeted

    let private phiControl defaultBasis (model: OrbitState) =
        let phi = model.phi + 2.0 * Math.PI + 0.3
        let targeted = OrbitController.update model (SetTargetPhi phi)
        Expect.equal targeted.targetPhi phi "Azimuth target is not clamped"
        Expect.equal (targeted.targetTheta, targeted.targetRadius, targeted.targetCenter, targeted.targetPan)
            (model.targetTheta, model.targetRadius, model.targetCenter, model.targetPan) "Other targets after azimuth change"
        Expect.equal (pose targeted) (pose model) "Azimuth camera motion is deferred"
        render defaultBasis model phi model.theta targeted

    let private dragControl defaultBasis (model: OrbitState) delta expected =
        let start = V2i(17, 23)
        let destination = start + V2i(0, delta)
        let dragging = OrbitController.update model (MouseDown(MouseButtons.Left, start))
        let targeted = OrbitController.update dragging (MouseMove destination)
        checkThetaTarget dragging expected targeted
        Expect.equal targeted.dragStart (Some destination) "Drag advances its origin"
        Expect.isFalse targeted.panning "Vertical rotation is not panning"
        render defaultBasis model model.phi expected targeted

    [<Tests>]
    let tests = testList "OrbitController Tests" [
        test "Elevation targeting changes only the clamped target" {
            for _, model in fixtures do
                for request, expected in requests model do target model request expected
        }
        test "Rendered elevation follows spherical geometry without changing azimuth" {
            for defaultBasis, model in fixtures do
                for request, expected in requests model do camera defaultBasis model request expected
        }
        test "Azimuth targeting retains its unclamped and deferred behavior" {
            for defaultBasis, model in fixtures do phiControl defaultBasis model
        }
        test "Vertical dragging retains elevation clamping and deferred motion" {
            for defaultBasis, model in fixtures do
                for delta, expected in [25, model.theta + 0.25; -400, model.thetaRange.Min; 400, model.thetaRange.Max] do
                    dragControl defaultBasis model delta expected
        }
    ]
