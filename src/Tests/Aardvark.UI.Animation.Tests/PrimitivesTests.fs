namespace Aardvark.UI.Animation.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Animation
open Expecto

module ``Primitives Tests`` =
    open Animation.Primitives.Utilities

    module private CatmullRom =

        let empty =
            test "Empty" {
                let s = Splines.catmullRom Vec.distance 0.1 Array.empty<V3d>
                Expect.isEmpty s "Invalid spline segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let s = Splines.catmullRom Vec.distance 0.1 [| p |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    Expect.equal r p $"Invalid result for t = {t}"
            }

        let pair =
            test "Pair" {
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(5, 6, 7)
                let s = Splines.catmullRom Vec.distance 0.1 [| p0; p1 |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    let e = lerp p0 p1 t
                    Expect.v3dClose Accuracy.high r e $"Invalid result for t = {t}"
            }

        let pairClose =
            test "Pair Close" {
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + Epsilon, p0.YZ)
                let s = Splines.catmullRom Vec.distance 0.1 [| p0; p1 |]
                Expect.hasLength s 1 "Invalid spline segment count"

                let n = 32
                for i = 0 to n - 1 do
                    let t = float i / float (n - 1)
                    let r = s.[0].Evaluate t
                    let e = lerp p0 p1 t
                    Expect.v3dClose Accuracy.high r e $"Invalid result for t = {t}"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let pts = [| p0; p1; p2; p3 |]
                let s = Splines.catmullRom Vec.distance 0.1 pts
                Expect.hasLength s 3 "Invalid spline segment count"

                let n = 32
                for j = 0 to 2 do
                    for i = 0 to n - 1 do
                        let t = float i / float (n - 1)
                        let r = s.[j].Evaluate t
                        let e = lerp pts.[j] pts.[j + 1] t
                        Expect.v3dClose Accuracy.high r e $"Invalid result in segment {j} for t = {t} (j = {i})"
            }

    module private LinearPath =
        
        let empty =
            test "Empty" {
                let a = Animation.Primitives.linearPath' Vec.distance Array.empty<V3d>
                Expect.isEmpty a "Unexpected segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let a = Animation.Primitives.linearPath' Vec.distance [| p |]
                Expect.hasLength a 1 "Invalid segment count"

                let d =
                    match a.[0].Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.equal d 0.0 "Invalid duration"
            }

        let simple =
            test "Simple" {
                let a = Animation.Primitives.linearPath Vec.distance [ V3d(1, 2, 3); V3d(-2, 5, -7); V3d(21, -6, 0); ]

                let d =
                    match a.Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.floatClose Accuracy.high d 1.0 "Invalid duration"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let a = Animation.Primitives.linearPath Vec.distance [| p0; p1; p2; p3 |]

                let d =
                    match a.Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.equal d 0.0 "Invalid duration"
            }

    module private SmoothPath =

        let empty =
            test "Empty" {
                let a = Animation.Primitives.smoothPath' Vec.distance 0.1 Array.empty<V3d>
                Expect.isEmpty a "Unexpected segment count"
            }

        let single =
            test "Single" {
                let p = V3d(1, 2, 3)
                let a = Animation.Primitives.smoothPath' Vec.distance 0.1 [| p |]
                Expect.hasLength a 1 "Invalid segment count"

                let d =
                    match a.[0].Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.equal d 0.0 "Invalid duration"
            }

        let simple =
            test "Simple" {
                let a = Animation.Primitives.smoothPath Vec.distance 0.1 [ V3d(1, 2, 3); V3d(-2, 5, -7); V3d(21, -6, 0); ]

                let d =
                    match a.Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.floatClose Accuracy.high d 1.0 "Invalid duration"
            }

        let degenerated =
            test "Degenerated" {
                let eps = Epsilon * Epsilon * 0.5
                let p0 = V3d(1, 2, 3)
                let p1 = V3d(p0.X + eps, p0.YZ)
                let p2 = V3d(p1.X, p1.Y + eps, p1.Z)
                let p3 = V3d(p2.XY, p2.Z + eps)
                let a = Animation.Primitives.smoothPath Vec.distance 0.1 [| p0; p1; p2; p3 |]

                let d =
                    match a.Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.equal d 0.0 "Invalid duration"
            }

    module Camera =

        let coincidingLocations =
            test "Coinciding Locations" {
                use _ = Animator.initTest()

                let eps = Epsilon * Epsilon * 0.5
                let c0 = CameraView.look V3d.Zero V3d.XAxis V3d.ZAxis
                let c1 = CameraView.look (V3d(eps, 0.0, 0.0)) V3d.YAxis V3d.ZAxis
                let c2 = CameraView.look (-V3d(eps, 0.0, 0.0)) (V3d(1.0, 2.0, 3.0).Normalized) V3d.ZAxis
                let c3 = CameraView.look V3d.Zero (V3d(1.0, 1.0, 1.0).Normalized) V3d.ZAxis

                let a =
                    Animation.Camera.smoothPath 0.1 [| c0; c1; c2; c3 |]

                let d =
                    match a.Duration with
                    | Duration.Finite d -> d.TotalSeconds
                    | _ -> failwith "Infinite duration"

                Expect.floatClose Accuracy.high d 0.0 "Invalid duration"
            }

    [<Tests>]
    let tests =
        testList "Primitives" [
            testList "Catmull-Rom" [
                CatmullRom.empty
                CatmullRom.single
                CatmullRom.pair
                CatmullRom.pairClose
                CatmullRom.degenerated
            ]

            testList "Linear Path" [
                LinearPath.empty
                LinearPath.single
                LinearPath.simple
                LinearPath.degenerated
            ]

            testList "Smooth Path" [
                SmoothPath.empty
                SmoothPath.single
                SmoothPath.simple
                SmoothPath.degenerated
            ]

            testList "Camera" [
                Camera.coincidingLocations
            ]
        ]