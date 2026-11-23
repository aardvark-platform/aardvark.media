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

    let private indeterminateRays test = [
        for deviation in [0.0; 1e-8; 3e-8] do
            for sign in [-1.0; 1.0] do
                for magnitude in [0.25; 1.0; 16.0] do
                    let direction = sign * magnitude * (test.direction + deviation * test.normal)
                    yield deviation, direction, RayPart(FastRay3d(Ray3d(V3d(9.0, -4.0, 7.0), direction)), 0.0, 10.0)
    ]

    let private positionAfter test delta =
        let direction =
            if test.rotated && test.mode = TrafoMode.Local then
                V3d(test.direction.Z, test.direction.X, test.direction.Y)
            else test.direction
        V3d(4.0, -2.0, 7.0) + delta * direction

    let private checkActive test delta (model : Transformation) =
        Expect.equal model.pose (initial test).pose "Drag retains the committed pose"
        Expect.equal (model.mode, model.hovered, model.scale) (test.mode, Some Axis.Y, 1.7) "Drag retains other state"
        Expect.equal model.workingPose.position (delta * test.direction) "Working displacement from original grab"
        Expect.equal model.workingPose.rotation Rot3d.Identity "Working rotation"
        Expect.equal model.workingPose.scale V3d.One "Working scale"
        Expect.equal model.grabbed.Value.offset 2.0 "Original grab anchor"
        Expect.equal model.grabbed.Value.axis test.axis "Original axis"
        checkTrafo test (positionAfter test delta) model.previewTrafo

    let private checkCommitted test delta (model : Transformation) =
        checkVector "Released position" (positionAfter test delta) model.pose.position
        Expect.equal model.pose.rotation (initial test).pose.rotation "Release keeps rotation"
        Expect.equal model.pose.scale test.scale "Release keeps scale"
        Expect.equal model.workingPose Pose.identity "Release resets working pose"
        Expect.isNone model.grabbed "Release clears grab"
        Expect.equal (model.mode, model.hovered, model.scale) (test.mode, Some Axis.Y, 1.7) "Release keeps other state"
        checkTrafo test (positionAfter test delta) model.previewTrafo

    // Collect matrix failures so a first-axis failure does not hide other axes/modes.
    let private checkIndeterminate grab =
        let failures = ResizeArray<string>()
        for item in cases do
            for deviation, direction, bad in indeterminateRays item do
                try
                    let original = initial item
                    let grabbed = TranslateController.updateController original (TrafoController.Grab(ray item 2.0, item.axis))
                    let moved = TranslateController.updateController grabbed (TrafoController.MoveRay(ray item 2.5))
                    checkActive item 0.5 moved
                    Expect.equal (TranslateController.closestT bad item.axis) 0.0 "Pinned solver's indeterminate fallback remains public"
                    if deviation > 0.0 then
                        Expect.isFalse (direction.IsParallelTo item.direction) "Roundoff degeneracy is not IsParallelTo"
                    if grab then
                        for model in [original; moved] do
                            let ignored = TranslateController.updateController model (TrafoController.Grab(bad, item.axis))
                            Expect.isTrue (obj.ReferenceEquals(ignored, model)) "Indeterminate Grab must keep the complete idle/active model"
                    else
                        let ignored = TranslateController.updateController moved (TrafoController.MoveRay bad)
                        Expect.isTrue (obj.ReferenceEquals(ignored, moved)) "Indeterminate MoveRay must keep the complete last valid model"
                        let opposite = RayPart(FastRay3d(Ray3d(bad.Ray.Ray.Origin, -direction)), 0.0, 10.0)
                        let ignored = TranslateController.updateController ignored (TrafoController.MoveRay opposite)
                        Expect.isTrue (obj.ReferenceEquals(ignored, moved)) "Antiparallel follow-up retains the same state"
                        Expect.isTrue (obj.ReferenceEquals(ignored.grabbed.Value, grabbed.grabbed.Value)) "Keep original grab identity"
                        checkActive item 0.5 ignored
                        checkCommitted item 0.5 (TranslateController.updateController ignored TrafoController.Release)
                        let resumed = TranslateController.updateController ignored (TrafoController.MoveRay(ray item 3.0))
                        checkActive item 1.0 resumed
                        checkCommitted item 1.0 (TranslateController.updateController resumed TrafoController.Release)
                with e -> failures.Add $"{item.name}, deviation={deviation}, direction={direction}: {e.Message}"
        Expect.isEmpty failures (String.concat "\n" failures)

    let private checkSolvable () =
        for item in cases do
            for deviation in [5e-8; 1e-7; 1e-5] do
                for magnitude in [0.25; 1.0; 16.0] do
                    for sign in [-1.0; 1.0] do
                        let direction = sign * magnitude * (item.direction + deviation * item.normal)
                        let near coordinate = RayPart(FastRay3d(Ray3d(coordinate * item.direction, direction)), 0.0, 10.0)
                        // Binary powers keep this ill-conditioned native solve exact.
                        let grabbed = TranslateController.updateController (initial item) (TrafoController.Grab(near 2.0, item.axis))
                        Expect.equal grabbed.grabbed.Value.offset 2.0 "Nearby solvable Grab is accepted"
                        for coordinate, delta in [4.0, 2.0; 1.0, -1.0; 0.0, -2.0] do
                            Expect.equal (TranslateController.closestT (near coordinate) item.axis) coordinate "Keep public regular solve"
                            let moved = TranslateController.updateController grabbed (TrafoController.MoveRay(near coordinate))
                            checkActive item delta moved
                            checkCommitted item delta (TranslateController.updateController moved TrafoController.Release)

    [<Tests>]
    let tests =
        testList "Primitives.TrafoController.Translation" [
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
            test "Indeterminate grabs preserve idle and active state" { checkIndeterminate true }
            test "Indeterminate moves retain previews anchors release and recovery" { checkIndeterminate false }
            test "Nearby solvable and zero-coordinate drags retain regular results" { checkSolvable () }
        ]
