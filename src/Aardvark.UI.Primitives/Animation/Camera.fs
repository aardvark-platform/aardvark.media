namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aardvark.Rendering

[<AutoOpen>]
module AnimationCameraPrimitives =

    module Animation =

        module Camera =

            open Aether
            open Animation

            [<AutoOpen>]
            module private CameraViewExtensions =
                type CameraView with
                    member x.Orientation =
                        let frame = M33d.FromCols(x.Right, x.Forward, x.Up) |> Mat.Orthonormalized
                        Rot3d.FromM33d frame

                module CameraView =
                    let orient (location : V3d) (orientation : Rot3d) (sky : V3d) =
                        let frame = M33d.Rotation orientation
                        CameraView(sky, location, frame.C1, frame.C2, frame.C0)

            /// Creates an animation that interpolates between the camera views src and dst.
            let interpolate (src : CameraView) (dst : CameraView) : IAnimation<'Model, CameraView> =
                let animPos = Primitives.lerp src.Location dst.Location
                let animOri = Primitives.slerp src.Orientation dst.Orientation

                (animPos, animOri)
                ||> Animation.map2 (fun pos ori -> CameraView.orient pos ori dst.Sky)

            /// Creates an animation that interpolates the camera view variable specified by
            /// the lens to dst. The animation is linked to that variable.
            let interpolateTo (lens : Lens<'Model, CameraView>) (dst : CameraView) (model : 'Model) =
                interpolate (model |> Optic.get lens) dst
                |> Animation.link lens

            /// Creates an animation that orbits the camera view around the center and axis by the given angle.
            let orbit (center : V3d) (normalizedAxis : V3d) (angleInRadians : float) (src : CameraView) : IAnimation<'Model, CameraView> =
                Animation.create (fun t ->
                    let angle = t |> lerp 0.0 angleInRadians
                    let rotation = Rot3d.Rotation(normalizedAxis, angle)
                    let location = center + rotation * (src.Location - center)
                    CameraView.lookAt location center src.Sky
                    //let orientation = rotation * src.Orientation
                    //CameraView.orient location orientation src.Sky
                )

            /// Creates an animation that orbits the camera view variable specified by
            /// the lens around the center and axis by the given angle. The animation is linked to that variable.
            let orbitTo (lens : Lens<'Model, CameraView>) (center : V3d) (normalizedAxis : V3d) (angleInRadians : float) (model : 'Model) =
                orbit center normalizedAxis angleInRadians (model |> Optic.get lens)
                |> Animation.link lens