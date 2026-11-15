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
                          upSpeed = 0.0; downSpeed = 0.0
                          look = false; zoom = false; pan = false
                          forward = false; backward = false; left = false; right = false; upward = false; downward = false }
        Expect.equal actual expected $"{context}: only focus-loss reset fields change"
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

    let private trigger upward value state =
        FreeFlyController.update state (if upward then FreeFlyController.PanUp value else FreeFlyController.PanDown value)

    let private neutral (state : CameraControllerState) =
        { state with animating = true; moveVec = V3d.Zero; upSpeed = 0.0; downSpeed = 0.0 }

    let private checkMotion context upSpeed downSpeed (expected : V3d) (before : CameraControllerState) (actual : CameraControllerState) =
        Expect.isTrue (actual.moveVec.IsFinite && (actual.moveVec - expected).Length < 1e-12) $"{context}: expected movement {expected}, got {actual.moveVec}"
        Expect.equal (actual.upSpeed, actual.downSpeed) (upSpeed, downSpeed) $"{context}: remembered trigger values"
        Expect.equal { actual with moveVec = before.moveVec; upSpeed = before.upSpeed; downSpeed = before.downSpeed } before
            $"{context}: trigger handling preserves unrelated state"
        Expect.isTrue (obj.ReferenceEquals(actual.view, before.view)) $"{context}: trigger camera identity during animation"
        Expect.isTrue (obj.ReferenceEquals(actual.freeFlyConfig, before.freeFlyConfig)) $"{context}: trigger configuration identity"

    let private checkTriggers context state =
        let seed = neutral state
        for upward in [true; false] do
            for value in [0.25; 0.6; 1.0] do
                let expected = V3d(0.0, (if upward then value else -value), 0.0)
                let upSpeed, downSpeed = if upward then value, 0.0 else 0.0, value
                let held = trigger upward value seed
                checkMotion context upSpeed downSpeed expected seed held
                let adjusted = trigger upward 0.125 held
                let adjustedDirection = V3d(0.0, (if upward then 0.125 else -0.125), 0.0)
                checkMotion context (if upward then 0.125 else 0.0) (if upward then 0.0 else 0.125) adjustedDirection held adjusted
                checkMotion context 0.0 0.0 V3d.Zero adjusted (trigger upward 0.0 adjusted)
                for blurCount in [1; 2] do
                    let mutable blurred = held
                    for _ in 1 .. blurCount do blurred <- blur blurred
                    let released = trigger upward 0.0 blurred
                    checkMotion $"{context}, {upward}, {value}, release" 0.0 0.0 V3d.Zero blurred released
                    checkMotion context 0.0 0.0 V3d.Zero released (trigger upward 0.0 released)
                    let resumed = trigger upward value blurred
                    checkMotion $"{context}, {upward}, {value}, reengage" upSpeed downSpeed expected blurred resumed
                    checkMotion context upSpeed downSpeed expected resumed (trigger upward value resumed)
                    let stopped = trigger upward 0.0 resumed
                    checkMotion context 0.0 0.0 V3d.Zero resumed stopped
                    checkReset context stopped (blur stopped)

    let private checkBothTriggers context state =
        let seed = neutral state
        for upValue, downValue in [0.6, 0.4; 1.0, 1.0; 0.25, 1.0; 1.0, 0.25] do
            for upwardFirst in [true; false] do
                let held =
                    if upwardFirst then seed |> trigger true upValue |> trigger false downValue
                    else seed |> trigger false downValue |> trigger true upValue
                checkMotion context upValue downValue (V3d(0.0,upValue-downValue,0.0)) seed held
                for releaseUpFirst in [true; false] do
                    let blurred = held |> blur |> blur
                    let first = trigger releaseUpFirst 0.0 blurred
                    checkMotion $"{context}: first delayed trigger release" 0.0 0.0 V3d.Zero blurred first
                    let second = trigger (not releaseUpFirst) 0.0 first
                    checkMotion $"{context}: second delayed trigger release" 0.0 0.0 V3d.Zero first second
                    let resumed = trigger true upValue second
                    checkMotion context upValue 0.0 (V3d(0.0,upValue,0.0)) second resumed
                    let resumedBoth = trigger false downValue resumed
                    checkMotion context upValue downValue (V3d(0.0,upValue-downValue,0.0)) resumed resumedBoth
                    let remaining = trigger releaseUpFirst 0.0 resumedBoth
                    let up, down = if releaseUpFirst then 0.0, downValue else upValue, 0.0
                    checkMotion context up down (V3d(0.0,up-down,0.0)) resumedBoth remaining
                    checkMotion context 0.0 0.0 V3d.Zero remaining (trigger (not releaseUpFirst) 0.0 remaining)

    let private checkTriggerKeyboard context state =
        let keys = [false, Keys.W, V3d.OOI; false, Keys.S, -V3d.OOI; false, Keys.A, -V3d.IOO; false, Keys.D, V3d.IOO
                    true, Keys.W, V3d.OIO; true, Keys.S, -V3d.OIO]
        for upward in [true; false] do
            for value in [0.6; 1.0] do
                for shift, key, direction in keys do
                    for releaseFirst in [true; false] do
                        let blurred = neutral state |> trigger upward value |> blur
                        let pressed =
                            if releaseFirst then blurred |> trigger upward 0.0 |> down shift key
                            else blurred |> down shift key |> trigger upward 0.0
                        Expect.isTrue ((pressed.moveVec-direction).Length < 1e-12) $"{context}: delayed trigger release cannot offset {key} movement"
                        Expect.equal (pressed.upSpeed,pressed.downSpeed) (0.0,0.0) $"{context}: keyboard does not restore trigger speeds"
                        Expect.equal (flags pressed) (not shift && key=Keys.W, not shift && key=Keys.S, key=Keys.A, key=Keys.D, shift && key=Keys.W, shift && key=Keys.S)
                            $"{context}: keyboard flags are independent of released triggers"
                        let released = up (not shift) key pressed
                        Expect.isTrue (released.moveVec.Length < 1e-12) $"{context}: keyboard release stops movement"
                        Expect.equal (flags released) stopped $"{context}: keyboard release clears its own flag"
                        Expect.isTrue (obj.ReferenceEquals(released.view, blurred.view)) $"{context}: keyboard camera identity during animation"
                        Expect.isTrue (obj.ReferenceEquals(released.freeFlyConfig, blurred.freeFlyConfig)) $"{context}: keyboard configuration identity"
                        checkReset context released (blur released)

    let private renderedTriggers () =
        lock FreeFlyController.sw (fun () ->
            let running = FreeFlyController.sw.IsRunning
            FreeFlyController.sw.Stop()
            try
                let now = FreeFlyController.sw.Elapsed.TotalSeconds
                for view in [CameraView.lookAt (V3d(4.0,-2.0,7.0)) V3d.Zero V3d.ZAxis
                             CameraView.lookAt (V3d(-3.0,6.0,2.0)) V3d.Zero V3d.YAxis] do
                    let seed = { (neutral FreeFlyController.initial) with view = view; freeFlyConfig = { FreeFlyConfig.initial with moveSensitivity = 0.0 } }
                    for upward in [true; false] do
                        for value in [0.6; 1.0] do
                            let blurred = seed |> trigger upward value |> blur
                            let released = trigger upward 0.0 blurred
                            let stopped = FreeFlyController.update { released with lastTime = Some (now - 0.02) } FreeFlyController.Rendered
                            Expect.isTrue ((stopped.view.Location-view.Location).Length < 1e-10) "Rendered must not move in the opposite direction after a delayed release"
                            Expect.isTrue (obj.ReferenceEquals(stopped.view, view)) "Neutral rendering retains the camera object"
                            let resumed = trigger upward value blurred
                            let moved = FreeFlyController.update { resumed with lastTime = Some (now - 0.02) } FreeFlyController.Rendered
                            let expected = view.Location + (if upward then 1.0 else -1.0) * value * 0.02 * view.Sky
                            Expect.isTrue (moved.view.Location.IsFinite && (moved.view.Location-expected).Length < 1e-10) "Identical reengagement renders movement in the requested direction"
                            Expect.equal (moved.view.Forward,moved.view.Up,moved.view.Right,moved.view.Sky) (view.Forward,view.Up,view.Right,view.Sky) "Translation preserves the camera frame"
                            Expect.isTrue (obj.ReferenceEquals(moved.freeFlyConfig, seed.freeFlyConfig)) "Rendered retains configuration identity"
            finally
                if running then FreeFlyController.sw.Start())

    [<Tests>]
    let tests =
        testList "Primitives.Camera.Controller.FreeFly" [
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
            test "Trigger release and identical reengagement after Blur start from neutral" {
                for name, state in states do checkTriggers name state
            }
            test "Both triggers reset together regardless of press and release order" {
                for name, state in states do checkBothTriggers name state
            }
            test "Keyboard input after trigger Blur is independent of delayed releases" {
                for name, state in states do checkTriggerKeyboard name state
            }
            test "Rendered stops released triggers and moves identical reengagement correctly" { renderedTriggers() }
        ]
