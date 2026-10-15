namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.UI.Trafos
open Expecto

module ``TranslationController Tests`` =

    type private Case =
        { name : string; mode : TrafoMode; scale : V3d; rotated : bool
          axis : Axis; direction : V3d; normal : V3d; positions : V3d list }

    let private cases = [
        for scaleName, scale in ["unit", V3d.III; "nonuniform", V3d(2.0, 3.0, 4.0)] do
            for rotated in [false; true] do
                for mode in [TrafoMode.Local; TrafoMode.Global] do
                    for axis, direction, normal, straight, cyclic in [
                        Axis.X, V3d.XAxis, V3d.YAxis,
                            [V3d(5.0, -2.0, 7.0); V3d(2.0, -2.0, 7.0)],
                            [V3d(4.0, -1.0, 7.0); V3d(4.0, -4.0, 7.0)]
                        Axis.Y, V3d.YAxis, V3d.ZAxis,
                            [V3d(4.0, -1.0, 7.0); V3d(4.0, -4.0, 7.0)],
                            [V3d(4.0, -2.0, 8.0); V3d(4.0, -2.0, 5.0)]
                        Axis.Z, V3d.ZAxis, V3d.XAxis,
                            [V3d(4.0, -2.0, 8.0); V3d(4.0, -2.0, 5.0)],
                            [V3d(5.0, -2.0, 7.0); V3d(2.0, -2.0, 7.0)]
                    ] do
                        yield
                            { name = $"{axis}, {mode}, {scaleName}, rotated={rotated}"
                              mode = mode; scale = scale; rotated = rotated; axis = axis
                              direction = direction; normal = normal
                              positions = if rotated && mode = TrafoMode.Local then cyclic else straight }
    ]

    let private initial test =
        let rotation =
            if test.rotated then Rot3d.Rotation(V3d(1.0, 1.0, 1.0).Normalized, 2.0 * Math.PI / 3.0)
            else Rot3d.Identity
        let pose = { position = V3d(4.0, -2.0, 7.0); rotation = rotation; scale = test.scale }
        { TrafoController.initial with
            pose = pose; previewTrafo = Pose.toTrafo pose; mode = test.mode
            hovered = Some Axis.Y; scale = 1.7
            // A new Grab must clear all of an earlier working pose.
            workingPose = pose }

    let private ray test coordinate =
        RayPart(FastRay3d(Ray3d(coordinate * test.direction + 3.0 * test.normal, -test.normal)), 0.0, 10.0)

    let private drag test =
        let original = initial test
        let grabbed = TranslateController.updateController original (TrafoController.Grab(ray test 2.0, test.axis))
        let first = TranslateController.updateController grabbed (TrafoController.MoveRay(ray test 3.0))
        let last = TranslateController.updateController first (TrafoController.MoveRay(ray test 0.0))
        original, grabbed, [first; last]

    let private points = [V3d.Zero; V3d.XAxis; V3d.YAxis; V3d.ZAxis; V3d(-2.0, 1.0, 0.5)]

    let private checkVector context (expected : V3d) (actual : V3d) =
        Expect.isLessThan (Vec.Distance(actual, expected)) 1e-10 context

    let private checkTrafo test (position : V3d) (actual : Trafo3d) =
        for point in points do
            // The known 120-degree rotation cycles X -> Y -> Z -> X.
            // Do not use Pose.toTrafo or a controller rotation helper as the oracle.
            let scaled = V3d(point.X * test.scale.X, point.Y * test.scale.Y, point.Z * test.scale.Z)
            let rotated = if test.rotated then V3d(scaled.Z, scaled.X, scaled.Y) else scaled
            checkVector $"{test.name}: forward transform at {point}" (position + rotated) (actual.Forward.TransformPos point)
            let relative = point - position
            let unrotated = if test.rotated then V3d(relative.Y, relative.Z, relative.X) else relative
            let inverse = V3d(unrotated.X / test.scale.X, unrotated.Y / test.scale.Y, unrotated.Z / test.scale.Z)
            checkVector $"{test.name}: inverse transform at {point}" inverse (actual.Backward.TransformPos point)
            checkVector $"{test.name}: inverse after forward" point (actual.Backward.TransformPos(actual.Forward.TransformPos point))
            checkVector $"{test.name}: forward after inverse" point (actual.Forward.TransformPos(actual.Backward.TransformPos point))

    let private checkPreview test index =
        let _, _, moves = drag test
        let moved = moves[index]
        let expected = test.positions[index]
        checkTrafo test expected moved.previewTrafo
        let released = TranslateController.updateController moved TrafoController.Release
        checkTrafo test expected released.previewTrafo
        for point in points do
            checkVector $"{test.name}: preview must not jump on Release"
                (moved.previewTrafo.Forward.TransformPos point) (released.previewTrafo.Forward.TransformPos point)

    let private checkState test =
        let original, grabbed, moves = drag test
        Expect.equal grabbed.pose original.pose $"{test.name}: Grab preserves committed pose"
        checkTrafo test original.pose.position grabbed.previewTrafo
        Expect.equal grabbed.workingPose Pose.identity $"{test.name}: Grab clears working pose"
        Expect.equal grabbed.grabbed.Value.offset 2.0 $"{test.name}: nonzero grab offset"
        Expect.equal grabbed.grabbed.Value.axis test.axis $"{test.name}: grabbed axis"
        Expect.isTrue grabbed.grabbed.Value.hit.IsNaN $"{test.name}: translation keeps the unused hit marker"
        for delta, moved in List.zip [1.0; -2.0] moves do
            Expect.equal moved.pose original.pose $"{test.name}: drag does not commit"
            Expect.equal moved.workingPose.position (delta * test.direction) $"{test.name}: displacement from original grab"
            Expect.equal moved.workingPose.rotation Rot3d.Identity $"{test.name}: working rotation unchanged"
            Expect.equal moved.workingPose.scale V3d.III $"{test.name}: working scale unchanged"
            Expect.isTrue (obj.ReferenceEquals(moved.grabbed.Value, grabbed.grabbed.Value)) $"{test.name}: grab retained"
            Expect.equal (moved.mode, moved.hovered, moved.scale) (original.mode, original.hovered, original.scale)
                $"{test.name}: other state retained"
        let released = TranslateController.updateController (List.last moves) TrafoController.Release
        checkVector $"{test.name}: committed position" (List.last test.positions) released.pose.position
        Expect.equal released.pose.rotation original.pose.rotation $"{test.name}: committed rotation unchanged"
        Expect.equal released.pose.scale original.pose.scale $"{test.name}: committed scale unchanged"
        checkTrafo test (List.last test.positions) released.previewTrafo
        Expect.isNone released.grabbed $"{test.name}: Release clears grab"
        Expect.equal released.workingPose Pose.identity $"{test.name}: Release clears working pose"
        Expect.equal (released.mode, released.hovered, released.scale) (original.mode, original.hovered, original.scale)
            $"{test.name}: Release retains other state"
        Expect.isTrue (obj.ReferenceEquals(released, TranslateController.updateController released TrafoController.Release))
            $"{test.name}: repeated Release is a no-op"

    let private checkNoGrab test =
        let original = initial test
        for action in [TrafoController.MoveRay(ray test 3.0); TrafoController.MoveRay(ray test 0.0); TrafoController.Release] do
            Expect.isTrue (obj.ReferenceEquals(original, TranslateController.updateController original action))
                $"{test.name}: no-grab action leaves all state unchanged"

    [<Tests>]
    let tests =
        testList "TranslationController Tests" [
            test "local previews and Release use rotated displacement without scale" {
                for item in cases do
                    if item.mode = TrafoMode.Local then
                        for index in [0; 1] do checkPreview item index
            }
            test "global previews and Release retain world-space displacement" {
                for item in cases do
                    if item.mode = TrafoMode.Global then
                        for index in [0; 1] do checkPreview item index
            }
            test "Grab, drag and Release retain pose components and reset working state" {
                for item in cases do checkState item
            }
            test "moves and Release without a grab leave all state unchanged" {
                for item in cases do checkNoGrab item
            }
        ]
