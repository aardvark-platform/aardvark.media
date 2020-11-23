namespace Aardvark.UI.Anewmation

open Aardvark.Base
open Aardvark.Rendering

[<AutoOpen>]
module AnimationCameraPrimitives =

    module Animation =

        module Camera =

            open Aether
            open Animation

            /// Creates an animation that interpolates between the camera views src and dst.
            let interpolate (src : CameraView) (dst : CameraView) : IAnimation<'Model, CameraView> =
                let getOrientation (view : CameraView) =
                    let frame = M33d.FromCols(view.Right, view.Forward, view.Up) |> Mat.Orthonormalized
                    Rot3d.FromM33d frame

                let animPos = Primitives.lerp src.Location dst.Location
                let animOri = Primitives.slerp (getOrientation src) (getOrientation dst)

                (animPos, animOri)
                |> uncurry (
                    Animation.map2 (fun pos rot ->
                        let frame = M33d.Rotation rot
                        CameraView.look pos frame.C1 frame.C2
                    )
                )

            /// Creates an animation that interpolates the camera view variable specified by
            /// the lens to dst. The animation is linked to that variable.
            let interpolateTo (lens : Lens<'Model, CameraView>) (dst : CameraView) (model : 'Model) =
                interpolate (model |> Optic.get lens) dst
                |> Animation.link lens