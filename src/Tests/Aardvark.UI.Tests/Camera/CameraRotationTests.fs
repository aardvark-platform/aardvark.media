namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Primitives
open Expecto

module ``Camera Rotation Tests`` =

    let private cameras = [
        "Y forward", CameraView.LookAt(V3d(2.0, -3.0, 5.0), V3d(2.0, -2.0, 5.0), V3d.OOI)
        "Z forward", CameraView.LookAt(V3d(-5.0, 4.0, 2.0), V3d(-5.0, 4.0, 3.0), V3d.OIO)
        "oblique", CameraView.LookAt(V3d(2.0, -3.0, 5.0), V3d(4.0, 2.0, 4.0), V3d.OOI)
        "oblique custom sky", CameraView.LookAt(V3d(-4.0, 2.0, 7.0), V3d(0.0, 6.0, 6.0), V3d(1.0, -1.0, 3.0))
    ]

    // The oracle matrices use sin/cos directly, not quaternion/Euler conversion.
    let private axes = [
        "X", Rot3d.RotationX, (fun angle ->
            let c, s = cos angle, sin angle
            M33d(1.0, 0.0, 0.0, 0.0, c, -s, 0.0, s, c))
        "Y", Rot3d.RotationY, (fun angle ->
            let c, s = cos angle, sin angle
            M33d(c, 0.0, s, 0.0, 1.0, 0.0, -s, 0.0, c))
        "Z", Rot3d.RotationZ, (fun angle ->
            let c, s = cos angle, sin angle
            M33d(c, -s, 0.0, s, c, 0.0, 0.0, 0.0, 1.0))
    ]

    let private closeVector context (actual: V3d) (expected: V3d) =
        // Small enough that a skipped 1.01E-9 rotation cannot satisfy the oracle.
        Expect.isTrue ((actual - expected).Length <= 5E-13) $"{context}: expected {expected}, got {actual}"

    let private checkFrame context (original: CameraView) (actual: CameraView) forward up right =
        Expect.equal actual.Location original.Location $"{context}: location"
        Expect.equal actual.Sky original.Sky $"{context}: sky"
        closeVector (context + ": forward") actual.Forward forward
        closeVector (context + ": up") actual.Up up
        closeVector (context + ": right") actual.Right right
        for direction in [actual.Forward; actual.Up; actual.Right] do
            Expect.isTrue (abs (direction.Length - 1.0) <= 5E-13) $"{context}: unit frame vectors"
        for a, b in [actual.Forward, actual.Up; actual.Forward, actual.Right; actual.Up, actual.Right] do
            Expect.isTrue (abs (Vec.dot a b) <= 5E-13) $"{context}: perpendicular frame vectors"
        closeVector (context + ": handedness") (Vec.cross actual.Forward actual.Up) actual.Right

    let private rotate context (camera: CameraView) rotation (matrix: M33d) =
        let result = FreeFlyController.transformRot camera rotation
        Expect.isFalse (obj.ReferenceEquals(result, camera)) $"{context}: a non-tiny rotation must not return the unchanged camera"
        checkFrame context camera result (matrix * camera.Forward) (matrix * camera.Up) (matrix * camera.Right)

    let private noOp context (camera: CameraView) rotation =
        let result = FreeFlyController.transformRot camera rotation
        Expect.isTrue (obj.ReferenceEquals(result, camera)) $"{context}: return the original camera reference"
        checkFrame context camera result camera.Forward camera.Up camera.Right

    let private checkNoOps () =
        for cameraName, camera in cameras do
            noOp (cameraName + ": identity") camera Rot3d.Identity
            for axisName, rotation, _ in axes do
                for angle in [-1E-9; -0.5E-9; 0.5E-9; 1E-9] do
                    noOp $"{cameraName}: {axisName} {angle}" camera (rotation angle)
            // Each component is tiny even though their combined angle exceeds 1E-9.
            for angles in [V3d(0.75E-9); V3d(-0.75E-9); V3d(0.75E-9, -0.75E-9, 0.75E-9)] do
                noOp $"{cameraName}: combined {angles}" camera (Rot3d.RotationEuler angles)

    let private checkRotations angles =
        for cameraName, camera in cameras do
            for axisName, rotation, matrix in axes do
                for angle in angles do
                    rotate $"{cameraName}: {axisName} {angle}" camera (rotation angle) (matrix angle)

    [<Tests>]
    let tests =
        testList "Camera Rotation Tests" [
            test "Negative axis rotations change the camera frame" {
                checkRotations [-1.0; -0.25]
            }
            test "Positive axis rotations retain their transformation" {
                checkRotations [0.25; 1.0]
            }
            test "Identity and inclusive signed component threshold retain the camera reference" {
                checkNoOps()
            }
            test "Both signs above the component threshold rotate the frame" {
                checkRotations [-2E-9; -1.01E-9; 1.01E-9; 2E-9]
            }
        ]
