namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Expecto

module ``CameraUtilities Tests`` =

    // Literal orthonormal frames within Orbit's elevation limits and existing horizon policy.
    let private frames = [
        "aligned", CameraView(V3d.ZAxis, V3d(3.0, -4.0, 2.0), -V3d.XAxis, V3d.ZAxis, V3d.YAxis), 0.0
        "tilted", CameraView(V3d.ZAxis, V3d(-6.0, 2.0, 4.0), V3d(0.48, 0.64, -0.6), V3d(0.36, 0.48, 0.8), V3d(0.8, -0.6, 0.0)), asin 0.6
        "non-Z sky", CameraView(V3d.YAxis, V3d(7.0, -3.0, -5.0), V3d(0.48, 0.6, 0.64), V3d(-0.36, 0.8, -0.48), V3d(-0.8, 0.0, 0.6)), -asin 0.6
    ]

    // Expected radii are independent of the implementation's clamp.
    let private clamped = [0.01, 0.1; 0.05, 0.1; 40.1, 40.0; 100.0, 40.0]
    let private bounded = [0.1, 0.1; 1.0, 1.0; 4.0, 4.0; 40.0, 40.0]
    let private converters = ["Camera.toOrbitState", Camera.toOrbitState; "OrbitState.ofFreeFly", OrbitState.ofFreeFly]

    let private near context (expected : V3d) (actual : V3d) =
        Expect.isTrue ((actual - expected).Length <= 4E-12) $"{context}: expected {expected}, got {actual}"

    let private frame context location (expected : CameraView) (actual : CameraView) =
        near (context + ": location") location actual.Location
        near (context + ": forward") expected.Forward actual.Forward
        near (context + ": up") expected.Up actual.Up
        near (context + ": right") expected.Right actual.Right
        Expect.equal actual.Sky expected.Sky (context + ": sky")

    let private source view =
        { FreeFlyController.initial with
            view = view; look = true; pan = true; animating = true
            moveVec = V3d(1.0, 2.0, 3.0); targetPan = V2d(0.2, 0.3)
            targetZoom = 1.0; lastTime = Some 12.0; orbitCenter = Some(V3d(9.0, 8.0, 7.0)) }

    let private conversions radii =
        for name, convert in converters do
            for frameName, view, _ in frames do
                for requested, effective in radii do
                    let context = $"{name}, {frameName}, radius {requested}"
                    let state = convert requested (source view)
                    Expect.equal state.radius effective (context + ": effective radius")
                    frame context view.Location view state.view

    [<Tests>]
    let tests =
        testList "CameraUtilities" [
            test "Clamped orbit conversions preserve the camera frame" {
                conversions clamped
            }

            test "In-range and boundary conversions preserve the camera frame" {
                conversions bounded
            }

            test "Orbit construction retains radius bounds and frame basis" {
                let center = V3d(9.0, 11.0, -3.0)
                for name, view, theta in frames do
                    for requested, effective in clamped @ bounded do
                        let state = OrbitState.create' view.Right view.Sky center (-Constant.PiHalf) theta requested
                        Expect.equal state.radius effective $"{name}, {requested}: constructor still clamps"
                        frame $"{name}, {requested}: constructor" (center - view.Forward * effective) view state.view
            }

            test "Conversion back preserves the camera and FreeFly defaults" {
                let speeds = [
                    -1.0, -0.65, 0.4, 0.005
                    2.0, 2.35, 3.2, 0.04
                ]
                for name, convert in converters do
                    for frameName, view, _ in frames do
                        for radius in [0.01; 4.0; 100.0] do
                            let orbit = convert radius (source view)
                            for backName, back in ["Camera.toFreeFlyState", Camera.toFreeFlyState; "OrbitState.toFreeFly", OrbitState.toFreeFly] do
                                for speed, move, zoom, pan in speeds do
                                    let context = $"{name}, {frameName}, {radius}, {backName}, speed {speed}"
                                    let result = back speed orbit
                                    let config =
                                        { FreeFlyController.initial.freeFlyConfig with
                                            moveSensitivity = move; zoomMouseWheelSensitivity = zoom
                                            panMouseSensitivity = pan; dollyMouseSensitivity = pan }
                                    Expect.equal result { FreeFlyController.initial with view = orbit.view; freeFlyConfig = config }
                                        (context + ": retain all FreeFly defaults and speed settings")
                                    frame context view.Location view result.view
            }
        ]
