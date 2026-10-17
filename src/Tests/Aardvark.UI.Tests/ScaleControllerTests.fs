namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.UI.Trafos
open Expecto

module ``ScaleController Tests`` =

    type private Case =
        { name : string; pose : Pose; rotated : bool; mode : TrafoMode
          axis : Axis; direction : V3d; normal : V3d }

    let private rotation = Rot3d.Rotation(V3d(1.0, 1.0, 1.0).Normalized, 2.0 * Math.PI / 3.0)
    let private cases = [
        for name, position, scale, rotated in [
            "identity", V3d.Zero, V3d.One, false
            "translated/nonuniform", V3d(4.0, -2.0, 7.0), V3d(2.0, 3.0, 4.0), false
            "translated/rotated/nonuniform", V3d(4.0, -2.0, 7.0), V3d(2.0, 3.0, 4.0), true
        ] do
            for mode in [TrafoMode.Local; TrafoMode.Global] do
                for axis, direction, normal in [X, V3d.XAxis, V3d.YAxis; Y, V3d.YAxis, V3d.ZAxis; Z, V3d.ZAxis, V3d.XAxis] do
                    yield
                        { name = $"{name}, {axis}, {mode}"; mode = mode; rotated = rotated
                          pose = { position = position; scale = scale; rotation = if rotated then rotation else Rot3d.Identity }
                          axis = axis; direction = direction; normal = normal }
    ]

    let private initial test =
        { TrafoController.initial with
            pose = test.pose; previewTrafo = Pose.toTrafo test.pose; mode = test.mode
            hovered = Some Y; scale = 1.7
            // Scaling changes only the working scale, even if other working fields are set.
            workingPose = { position = V3d(8.0, 9.0, 10.0); rotation = rotation; scale = V3d(7.0, 8.0, 9.0) } }

    let private ray test coordinate =
        RayPart(FastRay3d(Ray3d(coordinate * test.direction + 3.0 * test.normal, -test.normal)), 0.0, 10.0)

    let private grab test coordinate model =
        ScaleController.updateController model (TrafoController.Grab(ray test coordinate, test.axis))

    let private move test coordinate model =
        ScaleController.updateController model (TrafoController.MoveRay(ray test coordinate))

    let private scaled axis factor (value : V3d) =
        match axis with
        | X -> V3d(value.X * factor, value.Y, value.Z)
        | Y -> V3d(value.X, value.Y * factor, value.Z)
        | Z -> V3d(value.X, value.Y, value.Z * factor)

    let private points = [V3d.Zero; V3d.XAxis; V3d.YAxis; V3d.ZAxis; V3d(-2.0, 1.0, 0.5)]
    let private checkVector context (expected : V3d) (actual : V3d) =
        let distance = Vec.Distance(actual, expected)
        Expect.isTrue (Double.IsFinite distance && distance < 1e-10) $"{context}: expected {expected}, got {actual}"

    let private checkTrafo test (scale : V3d) (actual : Trafo3d) =
        for point in points do
            // Independently scale components, then apply the known X -> Y -> Z -> X
            // rotation and translation. Do not use Pose/Trafo composition as the oracle.
            let value = V3d(point.X * scale.X, point.Y * scale.Y, point.Z * scale.Z)
            let value = if test.rotated then V3d(value.Z, value.X, value.Y) else value
            checkVector $"{test.name}: forward at {point}" (test.pose.position + value) (actual.Forward.TransformPos point)
            if scale.X <> 0.0 && scale.Y <> 0.0 && scale.Z <> 0.0 then
                let relative = point - test.pose.position
                let value = if test.rotated then V3d(relative.Y, relative.Z, relative.X) else relative
                let inverse = V3d(value.X / scale.X, value.Y / scale.Y, value.Z / scale.Z)
                checkVector $"{test.name}: inverse at {point}" inverse (actual.Backward.TransformPos point)
                checkVector $"{test.name}: inverse after forward" point (actual.Backward.TransformPos(actual.Forward.TransformPos point))
                checkVector $"{test.name}: forward after inverse" point (actual.Forward.TransformPos(actual.Backward.TransformPos point))

    let private checkPreview test factor =
        let dragged = initial test |> grab test 2.0 |> move test (1.0 + factor)
        checkTrafo test (scaled test.axis factor test.pose.scale) dragged.previewTrafo

    let private checkRelease test factor =
        let dragged = initial test |> grab test 2.0 |> move test (1.0 + factor)
        let released = ScaleController.updateController dragged TrafoController.Release
        let expected = scaled test.axis factor test.pose.scale
        Expect.equal released.pose.scale expected $"{test.name}: multiplicative committed scale"
        Expect.equal (released.pose.position, released.pose.rotation) (test.pose.position, test.pose.rotation)
            $"{test.name}: committed position/rotation retained"
        Expect.equal released.workingPose Pose.identity $"{test.name}: working state reset"
        Expect.isNone released.grabbed $"{test.name}: grab cleared"
        Expect.equal (released.mode, released.hovered, released.scale) (dragged.mode, dragged.hovered, dragged.scale)
            $"{test.name}: other state retained"
        checkTrafo test expected released.previewTrafo
        for point in points do
            checkVector $"{test.name}: release continuity" (dragged.previewTrafo.Forward.TransformPos point) (released.previewTrafo.Forward.TransformPos point)
        Expect.isTrue (obj.ReferenceEquals(released, ScaleController.updateController released TrafoController.Release))
            $"{test.name}: repeated Release is a no-op"
        // Even a stale cache must be refreshed from the new pose on Release.
        let stale = { dragged with previewTrafo = Trafo3d.Translation(V3d(-99.0, 33.0, 55.0)) }
        let refreshed = ScaleController.updateController stale TrafoController.Release
        checkTrafo test expected refreshed.previewTrafo

    let private checkRepeated test =
        let grabbed = initial test |> grab test 2.0
        let first = grabbed |> move test 3.0
        checkTrafo test (scaled test.axis 2.0 test.pose.scale) first.previewTrafo
        let last = first |> move test 2.5
        Expect.equal last.workingPose.scale (scaled test.axis 1.5 V3d.One) $"{test.name}: latest move uses original Grab"
        checkTrafo test (scaled test.axis 1.5 test.pose.scale) last.previewTrafo
        let committed = ScaleController.updateController last TrafoController.Release
        let next = committed |> grab test 4.0 |> move test 3.5
        let expected = scaled test.axis 0.75 test.pose.scale
        Expect.equal next.pose committed.pose $"{test.name}: second gesture does not commit early"
        checkTrafo test expected next.previewTrafo
        let released = ScaleController.updateController next TrafoController.Release
        Expect.equal released.pose.scale expected $"{test.name}: scales multiply across gestures"
        Expect.equal released.workingPose Pose.identity $"{test.name}: second release resets working state"
        Expect.isNone released.grabbed $"{test.name}: second release clears grab"
        checkTrafo test expected released.previewTrafo

    let private checkClamp test =
        let grabbed = initial test |> grab test 2.0
        for coordinate, factor in [1.25, 0.25; 1.0, 0.0; -5.0, 0.0] do
            let dragged = grabbed |> move test coordinate
            Expect.equal dragged.workingPose.scale (scaled test.axis factor V3d.One) $"{test.name}: existing drag lower clamp"
            Expect.equal dragged.pose test.pose $"{test.name}: clamp does not commit early"
            checkTrafo test (scaled test.axis factor test.pose.scale) dragged.previewTrafo
            let released = ScaleController.updateController dragged TrafoController.Release
            Expect.equal released.pose.scale (scaled test.axis factor test.pose.scale) $"{test.name}: clamped scale commits"
            checkTrafo test released.pose.scale released.previewTrafo

    let private checkState test =
        let original = initial test
        let grabbed = grab test 2.0 original
        Expect.equal grabbed.pose original.pose $"{test.name}: Grab preserves committed pose"
        Expect.equal grabbed.workingPose original.workingPose $"{test.name}: Grab preserves working state"
        Expect.equal grabbed.grabbed.Value.offset 2.0 $"{test.name}: nonzero Grab offset"
        Expect.equal grabbed.grabbed.Value.axis test.axis $"{test.name}: picked axis"
        Expect.isTrue grabbed.grabbed.Value.hit.IsNaN $"{test.name}: unused hit marker"
        checkTrafo test test.pose.scale grabbed.previewTrafo
        let clamped = move test -5.0 grabbed
        Expect.equal clamped.workingPose.scale (scaled test.axis 0.0 V3d.One) $"{test.name}: drag below -1 stays clamped"
        let collapsed = ScaleController.updateController clamped TrafoController.Release
        Expect.equal collapsed.pose.scale (scaled test.axis 0.0 test.pose.scale) $"{test.name}: zero scale still commits"
        Expect.equal collapsed.workingPose Pose.identity $"{test.name}: clamped release still resets"
        for factor in [1.5; 0.5; 0.0] do
            let moved = move test (1.0 + factor) grabbed
            Expect.equal moved.pose original.pose $"{test.name}: dragging preserves committed pose"
            Expect.equal moved.workingPose.scale (scaled test.axis factor V3d.One) $"{test.name}: selected working scale"
            Expect.equal (moved.workingPose.position, moved.workingPose.rotation) (original.workingPose.position, original.workingPose.rotation)
                $"{test.name}: other working fields retained"
            Expect.isTrue (obj.ReferenceEquals(moved.grabbed.Value, grabbed.grabbed.Value)) $"{test.name}: grab identity retained"
            Expect.equal (moved.mode, moved.hovered, moved.scale) (original.mode, original.hovered, original.scale)
                $"{test.name}: mode, hover and gizmo scale retained"
        for model in [original; grabbed] do
            for action in [TrafoController.Nop; TrafoController.SetMode TrafoMode.Global; TrafoController.SetMode TrafoMode.Local] do
                Expect.isTrue (obj.ReferenceEquals(model, ScaleController.updateController model action)) $"{test.name}: idle/mode action unchanged"
        for action in [TrafoController.MoveRay(ray test 3.0); TrafoController.Release] do
            Expect.isTrue (obj.ReferenceEquals(original, ScaleController.updateController original action)) $"{test.name}: no-grab action unchanged"
        let hovered = ScaleController.updateController original (TrafoController.Hover Z)
        Expect.equal hovered { original with hovered = Some Z } $"{test.name}: Hover changes only hover"
        Expect.equal (ScaleController.updateController hovered TrafoController.Unhover) { original with hovered = None }
            $"{test.name}: Unhover changes only hover"

    [<Tests>]
    let tests =
        testList "ScaleController Tests" [
            test "Drag previews scale before the committed pose" {
                for item in cases do
                    for factor in [1.5; 0.5] do checkPreview item factor
            }
            test "Release commits scale and refreshes the preview" {
                for item in cases do
                    for factor in [1.5; 0.5] do checkRelease item factor
            }
            test "Successive moves and gestures use their original grab" {
                for item in cases do checkRepeated item
            }
            test "Clamped drags preserve the zero scale boundary" {
                for item in cases do checkClamp item
            }
            test "Other pose fields, modes and idle actions are unchanged" {
                for item in cases do checkState item
            }
        ]
