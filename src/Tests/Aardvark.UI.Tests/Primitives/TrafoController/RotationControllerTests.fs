namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.UI.Trafos
open Expecto

module ``RotationController Tests`` =

    let private cases =
        let h = sqrt 0.5
        let s = sqrt 3.0 / 2.0
        // Explicit right-handed ring coordinates, independent of Axis.toV3d and RotateInto.
        let rings = [
            Axis.X, V3d.XAxis, (fun x y -> V3d(0.0, x, y))
            Axis.Y, V3d.YAxis, (fun x y -> V3d(y, 0.0, x))
            Axis.Z, V3d.ZAxis, (fun x y -> V3d(x, y, 0.0))
        ]
        let angles = [
            yield Math.PI / 4.0, h, h
            yield -Math.PI / 3.0, 0.5, -s
            yield Math.PI, -1.0, 0.0
            yield -Math.PI, -1.0, 0.0
            for epsilon in [1e-3; 1e-7] do
                for angle in [Math.PI - epsilon; -Math.PI + epsilon] do
                    yield angle, cos angle, sin angle
            // A move after the half turns must still be measured from the original Grab.
            yield 0.37, cos 0.37, sin 0.37
        ]
        [for mode in [TrafoMode.Global; TrafoMode.Local] do
            for axis, normal, point in rings do
                for x, y in [1.0, 0.0; 0.0, 1.0; 0.6, 0.8; -0.8, 0.6; 0.5, -0.5] do
                    let start = point x y
                    for finalAngle in [Math.PI; -Math.PI] do
                        let targets = [
                            for angle, c, s in angles do
                                yield angle, point (c * x - s * y) (s * x + c * y)
                            yield finalAngle, -start
                        ]
                        yield $"{axis}, {mode}, start {start}, release {finalAngle}", mode, axis, normal, start, targets]

    let private rayAt (normal : V3d) (point : V3d) =
        RayPart(FastRay3d(Ray3d(point + 2.0 * normal, -normal)), 0.0, 10.0)

    let private missedRay (normal : V3d) (tangent : V3d) =
        RayPart(FastRay3d(Ray3d(2.0 * normal, tangent)), 0.0, 10.0)

    let private initial mode =
        let pose =
            { position = V3d(2.0, -3.0, 5.0)
              rotation = Rot3d.Rotation(V3d(1.0, 2.0, -3.0).Normalized, 0.7)
              scale = V3d(2.0, 0.5, 3.0) }
        { TrafoController.initial with
            mode = mode; pose = pose; previewTrafo = Pose.toTrafo pose
            hovered = Some Axis.Y; scale = 1.7 }

    let private expectedRotation mode (normal : V3d) angle (pose : Pose) =
        let delta = Rot3d.Rotation(normal, angle)
        if mode = TrafoMode.Global then delta * pose.rotation
        else pose.rotation * delta

    let private expectedTrafo mode normal angle (pose : Pose) =
        // Axis-angle rotations, not RotateInto or the controller's composition helper.
        let rotation = expectedRotation mode normal angle pose
        Trafo3d.Scale pose.scale * Trafo3d.Rotation rotation * Trafo3d.Translation pose.position

    let private checkTrafo context (expected : Trafo3d) (actual : Trafo3d) =
        for point in [V3d.Zero; V3d.XAxis; V3d.YAxis; V3d.ZAxis; V3d(-1.5, 2.0, 0.25)] do
            let forward = Vec.Distance(actual.Forward.TransformPos point, expected.Forward.TransformPos point)
            let backward = Vec.Distance(actual.Backward.TransformPos point, expected.Backward.TransformPos point)
            Expect.isTrue (Double.IsFinite forward && Double.IsFinite backward) $"{context}: finite transforms"
            Expect.isLessThan forward 1e-10 $"{context}: forward transform at {point}"
            Expect.isLessThan backward 1e-10 $"{context}: inverse transform at {point}"

    let private checkRotation context expected (actual : Rot3d) =
        let norm = actual.W * actual.W + actual.X * actual.X + actual.Y * actual.Y + actual.Z * actual.Z
        Expect.isTrue (Double.IsFinite norm && abs (norm - 1.0) < 1e-12) $"{context}: unit quaternion"
        checkTrafo context (Trafo3d.Rotation expected) (Trafo3d.Rotation actual)

    let private drag mode axis normal start targets =
        let original = initial mode
        let grabbed = RotationController.updateController original (TrafoController.Grab(rayAt normal start, axis))
        let moves =
            targets |> List.mapFold (fun state (angle, point) ->
                let next = RotationController.updateController state (TrafoController.MoveRay(rayAt normal point))
                (angle, next), next) grabbed |> fst
        original, grabbed, moves

    [<Tests>]
    let tests =
        testList "Primitives.TrafoController.Rotation" [
            test "drag previews follow each target immediately" {
                for context, mode, axis, normal, start, targets in cases do
                    let original, _, moves = drag mode axis normal start targets
                    for angle, moved in moves do
                        checkTrafo $"{context}, target {angle}" (expectedTrafo mode normal angle original.pose) moved.previewTrafo
            }

            test "drag updates the working rotation without committing the pose" {
                for context, mode, axis, normal, start, targets in cases do
                    let original, grabbed, moves = drag mode axis normal start targets
                    Expect.equal grabbed.workingPose Pose.identity $"{context}: Grab resets working pose"
                    checkTrafo $"{context}: Grab preserves preview" original.previewTrafo grabbed.previewTrafo
                    Expect.equal grabbed.grabbed (Some { axis = axis; offset = 0.0; hit = start }) $"{context}: Grab hit"
                    for angle, moved in moves do
                        Expect.equal moved.pose original.pose $"{context}: committed pose stays unchanged"
                        Expect.equal moved.grabbed grabbed.grabbed $"{context}: original grab is retained"
                        Expect.equal (moved.mode, moved.hovered, moved.scale) (original.mode, original.hovered, original.scale)
                            $"{context}: mode, hover and handle scale are retained"
                        checkRotation $"{context}, angle {angle}: rotation from original hit" (Rot3d.Rotation(normal, angle)) moved.workingPose.rotation
                        let selectedAxis = (Trafo3d.Rotation moved.workingPose.rotation).Forward.TransformDir normal
                        Expect.isLessThan (Vec.Distance(selectedAxis, normal)) 1e-10 $"{context}: selected axis stays fixed"
                        Expect.equal moved.workingPose.position V3d.Zero $"{context}: working translation"
                        Expect.equal moved.workingPose.scale V3d.III $"{context}: working scale"
            }

            test "release commits without a preview jump and resets drag state" {
                for context, mode, axis, normal, start, targets in cases do
                    let original, _, moves = drag mode axis normal start targets
                    let angle, moved = List.last moves
                    let expected = expectedTrafo mode normal angle original.pose
                    let appliedPose, appliedTrafo = RotationController.applyRotation moved
                    let released = RotationController.updateController moved TrafoController.Release
                    for pose in [appliedPose; released.pose] do
                        checkRotation $"{context}: committed rotation" (expectedRotation mode normal angle original.pose) pose.rotation
                        Expect.equal pose.position original.pose.position $"{context}: committed translation"
                        Expect.equal pose.scale original.pose.scale $"{context}: committed scale"
                    checkTrafo $"{context}: public applyRotation" expected appliedTrafo
                    checkTrafo $"{context}: released preview" expected released.previewTrafo
                    checkTrafo $"{context}: release continuity" moved.previewTrafo released.previewTrafo
                    Expect.isNone released.grabbed $"{context}: Release clears the grab"
                    Expect.equal released.workingPose Pose.identity $"{context}: Release clears working pose"
                    Expect.equal (released.mode, released.hovered, released.scale) (moved.mode, moved.hovered, moved.scale)
                        $"{context}: Release retains non-drag state"
                    Expect.isTrue (obj.ReferenceEquals(released, RotationController.updateController released TrafoController.Release))
                        $"{context}: repeated Release is a no-op"
            }

            test "zero hit directions retain identity and allow subsequent drag moves" {
                for context, mode, axis, normal, start, targets in cases do
                    let original = initial mode
                    for from, target in [V3d.Zero, start; start, V3d.Zero; V3d.Zero, V3d.Zero] do
                        let grabbed = RotationController.updateController original (TrafoController.Grab(rayAt normal from, axis))
                        let moved = RotationController.updateController grabbed (TrafoController.MoveRay(rayAt normal target))
                        checkRotation $"{context}: zero direction" Rot3d.Identity moved.workingPose.rotation
                        checkTrafo $"{context}: zero-direction preview" original.previewTrafo moved.previewTrafo
                        let released = RotationController.updateController moved TrafoController.Release
                        checkTrafo $"{context}: zero-direction release" original.previewTrafo released.previewTrafo
                        checkRotation $"{context}: zero-direction committed rotation" original.pose.rotation released.pose.rotation
                        Expect.equal released.pose original.pose $"{context}: zero direction cannot commit a rotation"
                    let _, grabbed, moves = drag mode axis normal start targets
                    let _, halfTurn = List.last moves
                    let missed = RotationController.updateController halfTurn (TrafoController.MoveRay(missedRay normal start))
                    Expect.isTrue (obj.ReferenceEquals(halfTurn, missed)) $"{context}: a miss after a half turn retains the drag"
                    let zero = RotationController.updateController missed (TrafoController.MoveRay(rayAt normal V3d.Zero))
                    checkRotation $"{context}: zero resets a previous working rotation" Rot3d.Identity zero.workingPose.rotation
                    let angle, target = List.head targets
                    let continued = RotationController.updateController zero (TrafoController.MoveRay(rayAt normal target))
                    Expect.equal continued.grabbed grabbed.grabbed $"{context}: zero and missed moves retain the original Grab"
                    Expect.equal continued.pose original.pose $"{context}: continued drag is still uncommitted"
                    checkRotation $"{context}: continued rotation" (Rot3d.Rotation(normal, angle)) continued.workingPose.rotation
                    checkTrafo $"{context}: continued preview" (expectedTrafo mode normal angle original.pose) continued.previewTrafo
            }

            test "ungrabbed moves, missed intersections and NaN grabs cannot change the pose" {
                for context, mode, axis, normal, start, targets in cases do
                    let original, _, moves = drag mode axis normal start targets
                    let _, target = List.head targets
                    for action in [TrafoController.MoveRay(rayAt normal target); TrafoController.Release] do
                        Expect.isTrue (obj.ReferenceEquals(original, RotationController.updateController original action))
                            $"{context}: ungrabbed action is a no-op"
                    let _, moved = List.last moves
                    let missed = RotationController.updateController moved (TrafoController.MoveRay(missedRay normal start))
                    Expect.isTrue (obj.ReferenceEquals(moved, missed)) $"{context}: missed MoveRay is a no-op"
                    let grabbed = RotationController.updateController original (TrafoController.Grab(missedRay normal start, axis))
                    Expect.isTrue grabbed.grabbed.Value.hit.IsNaN $"{context}: parallel Grab records an invalid hit"
                    Expect.equal grabbed.workingPose Pose.identity $"{context}: invalid Grab resets working pose"
                    for _, point in targets do
                        let moved = RotationController.updateController grabbed (TrafoController.MoveRay(rayAt normal point))
                        Expect.isTrue (obj.ReferenceEquals(grabbed, moved)) $"{context}: MoveRay cannot use a NaN grab hit"
                    let released = RotationController.updateController grabbed TrafoController.Release
                    Expect.equal released.pose original.pose $"{context}: invalid drag preserves committed pose"
                    Expect.isNone released.grabbed $"{context}: Release clears an invalid grab"
                    Expect.equal released.workingPose Pose.identity $"{context}: Release resets an invalid drag"
            }
        ]
