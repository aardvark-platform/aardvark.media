namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Application
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Expecto

module ``FreeFlyController Tests`` =

    let private modifiers shift : KeyModifiers = { shift = shift; alt = false; ctrl = false }
    let private down shift key state = FreeFlyController.update state (FreeFlyController.KeyDown(modifiers shift, key))
    let private up shift key state = FreeFlyController.update state (FreeFlyController.KeyUp(modifiers shift, key))
    let private blur state = FreeFlyController.update state FreeFlyController.Blur
    let private flags (state : CameraControllerState) =
        state.forward, state.backward, state.left, state.right, state.upward, state.downward
    let private stopped = false, false, false, false, false, false
    let private vertical = [Keys.W, V3d(0.0, 1.0, 0.0); Keys.S, V3d(0.0, -1.0, 0.0)]

    let private states =
        let view = CameraView.lookAt (V3d(4.0, -2.0, 7.0)) V3d.Zero V3d.ZAxis
        let populated =
            { FreeFlyController.initial with
                view = view; freeFlyConfig = { FreeFlyConfig.initial with moveSensitivity = 2.5; panConstant = 0.125; touchScalesExponentially = false }
                lastTime = Some 12.5; animating = true; dragStart = V2i(30, 40); movePos = V2i(-9, 11)
                look = true; zoom = true; pan = true; dolly = true; isWheel = true; scrolling = true
                rotateVec = V3d(0.25, -0.5, 0.75); moveSpeed = 2.0; panSpeed = 3.0; upSpeed = 0.6; downSpeed = 0.4
                sensitivity = 1.25; scrollSensitivity = 1.5; zoomFactor = 0.2; panFactor = 0.3; rotationFactor = 0.4
                orbitCenter = Some (V3d(3.0, 2.0, 1.0)); stash = Some FreeFlyController.initial
                targetPhiTheta = V2d(0.1, -0.2); targetPan = V2d(0.3, -0.4); targetDolly = 2.0; targetZoom = 3.0
                targetJump = Some (CameraView.lookAt (V3d(1.0, 3.0, 8.0)) V3d.Zero V3d.YAxis) }
        ["initial", FreeFlyController.initial; "populated", populated]

    let private checkReset context (before : CameraControllerState) (actual : CameraControllerState) =
        Expect.equal actual.moveVec V3d.Zero $"{context}: stop movement"
        Expect.equal (flags actual) stopped $"{context}: clear every directional flag"
        let expected =
            { before with lastTime = None; moveVec = V3d.Zero; dragStart = V2i.Zero
                          look = false; zoom = false; pan = false
                          forward = false; backward = false; left = false; right = false; upward = false; downward = false }
        Expect.equal actual expected $"{context}: only the existing reset and vertical flags change"
        Expect.isTrue (obj.ReferenceEquals(actual.view, before.view)) $"{context}: camera identity"
        Expect.isTrue (obj.ReferenceEquals(actual.freeFlyConfig, before.freeFlyConfig)) $"{context}: configuration identity"

    let private checkFlags context state key =
        let held = down true key state
        checkReset context held (blur held)

    let private checkRelease context state key shift =
        let blurred = state |> down true key |> blur
        let released = up shift key blurred
        Expect.equal released.moveVec V3d.Zero $"{context}: delayed release cannot introduce movement"
        Expect.equal (flags released) stopped $"{context}: released flags remain clear"
        Expect.isTrue (obj.ReferenceEquals(released, blurred)) $"{context}: delayed release is a no-op"
        let repeated = up shift key released
        Expect.isTrue (obj.ReferenceEquals(released, repeated)) $"{context}: repeated release is a no-op"

    let private checkResume context state key direction blurCount =
        let mutable blurred = down true key state
        for _ in 1 .. blurCount do blurred <- blur blurred
        let pressed = down true key blurred
        Expect.equal pressed.moveVec direction $"{context}: a new shifted press moves vertically"
        Expect.equal (pressed.upward, pressed.downward) (key = Keys.W, key = Keys.S) $"{context}: new vertical key state"
        Expect.isTrue (obj.ReferenceEquals(pressed, down true key pressed)) $"{context}: repeated press cannot accumulate movement"
        let released = up false key pressed
        Expect.equal released.moveVec V3d.Zero $"{context}: release stops the new movement"
        Expect.equal (flags released) stopped $"{context}: release clears the new key state"

    let private checkAllHeld context state =
        let held =
            state |> down false Keys.W |> down false Keys.S |> down false Keys.A |> down false Keys.D
                  |> down true Keys.W |> down true Keys.S
        Expect.equal (flags held) (true, true, true, true, true, true) $"{context}: all keys were actually pressed"
        Expect.equal held.moveVec V3d.Zero $"{context}: opposing directions cancel"
        let blurred = blur held
        checkReset context held blurred
        checkReset context blurred (blur blurred)
        let mutable released = blurred
        for key in [Keys.W; Keys.S; Keys.A; Keys.D] do
            released <- up false key released
            Expect.equal released.moveVec V3d.Zero $"{context}: combined-key release cannot restart movement"
            Expect.equal (flags released) stopped $"{context}: combined-key flags remain clear"

    let private checkControls context state =
        let ordinary = [Keys.W, V3d(0.0, 0.0, 1.0); Keys.S, V3d(0.0, 0.0, -1.0); Keys.A, V3d(-1.0, 0.0, 0.0); Keys.D, V3d(1.0, 0.0, 0.0)]
        for key, direction in ordinary do
            let held = down false key state
            Expect.equal held.moveVec direction $"{context}: ordinary {key} direction"
            Expect.equal (held.upward, held.downward) (false, false) $"{context}: ordinary keys are not vertical"
            Expect.isTrue (obj.ReferenceEquals(held, down false key held)) $"{context}: repeated ordinary press is ignored"
            for shift in [false; true] do
                let released = up shift key held
                Expect.equal released.moveVec V3d.Zero $"{context}: ordinary release with either Shift state"
                Expect.equal (flags released) stopped $"{context}: ordinary release clears key state"
                Expect.isTrue (obj.ReferenceEquals(released, up shift key released)) $"{context}: repeated ordinary release is ignored"
                let blurred = blur held
                checkReset context held blurred
                Expect.isTrue (obj.ReferenceEquals(blurred, up shift key blurred)) $"{context}: ordinary release after Blur is ignored"
        for key, direction in vertical do
            let held = down true key state
            Expect.equal held.moveVec direction $"{context}: uninterrupted shifted direction"
            Expect.isTrue (obj.ReferenceEquals(held, down true key held)) $"{context}: uninterrupted repeated press is ignored"
            for shift in [false; true] do
                let released = up shift key held
                Expect.equal released.moveVec V3d.Zero $"{context}: uninterrupted shifted release"
                Expect.equal (flags released) stopped $"{context}: uninterrupted shifted flags"
        let opposed = state |> down true Keys.W |> down true Keys.S
        Expect.equal opposed.moveVec V3d.Zero $"{context}: uninterrupted vertical opposition"
        let descending = up true Keys.W opposed
        Expect.equal descending.moveVec (V3d(0.0, -1.0, 0.0)) $"{context}: releasing upward leaves downward movement"
        Expect.equal (up true Keys.S descending).moveVec V3d.Zero $"{context}: releasing both stops movement"
        checkReset context state (blur state)

    [<Tests>]
    let tests =
        testList "FreeFlyController Tests" [
            test "Blur clears every directional flag without broadening the reset" {
                for name, state in states do
                    for key, _ in vertical do checkFlags $"{name}, {key}" state key
                    checkAllHeld name state
            }
            test "Key releases after Blur cannot restart vertical movement" {
                for name, state in states do
                    for key, _ in vertical do
                        for shift in [false; true] do checkRelease $"{name}, {key}, shift={shift}" state key shift
            }
            test "Shifted movement resumes after single or repeated Blur" {
                for name, state in states do
                    for key, direction in vertical do
                        for count in [1; 2] do checkResume $"{name}, {key}, blurs={count}" state key direction count
            }
            test "Ordinary WASD and uninterrupted repeated key events retain behavior" {
                for name, state in states do checkControls name state
            }
        ]
