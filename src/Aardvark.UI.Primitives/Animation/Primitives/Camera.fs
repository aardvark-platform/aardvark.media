namespace Aardvark.UI.Animation

open Aardvark.Base
open Aardvark.Rendering

[<AutoOpen>]
module AnimationCameraPrimitives =

    module Animation =

        module Camera =

            open Aether
            open Animation

            /// Creates an animation that moves the camera view to the given location.
            let move (dst : V3d) (camera : CameraView) : IAnimation<'Model, CameraView> =
                Animation.create (fun t ->
                    let pos = t |> lerp camera.Location dst
                    camera |> CameraView.withLocation pos
                )

            /// Creates an animation that moves the camera view variable specified by
            /// the lens to the given location. The animation is linked to that variable via an observer with a progress callback.
            let moveTo (lens : Lens<'Model, CameraView>) (dst : V3d) (model : 'Model) =
                move dst (model |> Optic.get lens)
                |> Animation.link lens

            /// Creates an animation that moves the camera view to the given location, while looking at the given center.
            let moveAndLookAt (center : V3d) (dst : V3d) (camera : CameraView) =
               Animation.create (fun t ->
                   let location = t |> lerp camera.Location dst
                   CameraView.lookAt location center camera.Sky
               )

            /// Creates an animation that moves the camera view variable specified by
            /// the lens to the given location, while looking at the given center.
            /// The animation is linked to that variable via an observer with a progress callback.
            let moveToAndLookAt (lens : Lens<'Model, CameraView>) (center : V3d) (dst : V3d) (model : 'Model) =
               moveAndLookAt center dst (model |> Optic.get lens)
               |> Animation.link lens

            /// Creates an animation that rotates the camera view to face the given direction.
            let rotateDir (normalizedDirection : V3d) (camera : CameraView) : IAnimation<'Model, CameraView> =
                let src = camera.Orientation

                let dst =
                    let forward = normalizedDirection
                    let right = Vec.cross forward camera.Sky |> Vec.normalize
                    let up = Vec.cross right forward |> Vec.normalize
                    let frame = M33d.FromCols(right, forward, up)
                    Rot3d.FromM33d frame

                Animation.create (fun t ->
                    let orientation = Rot.SlerpShortest(src, dst, t)
                    CameraView.orient camera.Location orientation camera.Sky
                )

            /// Creates an animation that rotates the camera view variable specified by
            /// the lens to face the given direction. The animation is linked to that variable via an observer with a progress callback.
            let rotateDirTo (lens : Lens<'Model, CameraView>) (normalizedDirection : V3d) (model : 'Model) =
                rotateDir normalizedDirection (model |> Optic.get lens)
                |> Animation.link lens

            /// Creates an animation that rotates the camera view to face towards the given location.
            let rotateDynamic (center : V3d) (camera : Lens<'Model, CameraView>) : IAnimation<'Model, CameraView> =
                Animation.create' (fun m t ->
                    let camera = m |> Optic.get camera

                    let dst =
                        let forward = (center - camera.Location |> Vec.normalize)
                        let right = Vec.cross forward camera.Sky |> Vec.normalize
                        let up = Vec.cross right forward |> Vec.normalize
                        let frame = M33d.FromCols(right, forward, up)
                        Rot3d.FromM33d frame

                    let orientation = Rot.SlerpShortest(camera.Orientation, dst, t)
                    CameraView.orient camera.Location orientation camera.Sky
                )

            /// Creates an animation that rotates the camera view to face towards the given location.
            let rotate (center : V3d) (camera : CameraView) : IAnimation<'Model, CameraView> =
                camera |> rotateDir (center - camera.Location |> Vec.normalize)

            /// Creates an animation that rotates the camera view variable specified by
            /// the lens to face towards the given location. The animation is linked to that variable via an observer with a progress callback.
            let rotateTo (lens : Lens<'Model, CameraView>) (center : V3d) (model : 'Model) =
                rotate center (model |> Optic.get lens)
                |> Animation.link lens

            /// Creates an animation that interpolates between the camera views src and dst.
            let interpolate (src : CameraView) (dst : CameraView) : IAnimation<'Model, CameraView> =
                let animPos = Primitives.lerp src.Location dst.Location
                let animOri = Primitives.slerp src.Orientation dst.Orientation

                (animPos, animOri)
                ||> Animation.map2 (fun pos ori -> CameraView.orient pos ori dst.Sky)

            /// Creates an animation that interpolates the camera view variable specified by
            /// the lens to dst. The animation is linked to that variable via an observer with a progress callback.
            let interpolateTo (lens : Lens<'Model, CameraView>) (dst : CameraView) (model : 'Model) =
                interpolate (model |> Optic.get lens) dst
                |> Animation.link lens

            /// Creates an animation that orbits the camera view around the center and axis by the given angle.
            let orbit (center : V3d) (normalizedAxis : V3d) (angleInRadians : float) (camera : CameraView) : IAnimation<'Model, CameraView> =
                Animation.create (fun t ->
                    let angle = t |> lerp 0.0 angleInRadians
                    let rotation = Rot3d.Rotation(normalizedAxis, angle)
                    let location = center + rotation * (camera.Location - center)
                    CameraView.lookAt location center camera.Sky
                )

            /// Creates an animation that orbits the camera view variable specified by
            /// the lens around the center and axis by the given angle. The animation is linked to that variable via an observer with a progress callback.
            let orbitTo (lens : Lens<'Model, CameraView>) (center : V3d) (normalizedAxis : V3d) (angleInRadians : float) (model : 'Model) =
                orbit center normalizedAxis angleInRadians (model |> Optic.get lens)
                |> Animation.link lens

            /// Creates an animation that orbits the camera view around the center and axis by the given angle.
            let orbit' (center : IAnimation<'Model, V3d>) (normalizedAxis : V3d) (angleInRadians : float) (camera : CameraView) : IAnimation<'Model, CameraView> =
                let rotation =
                    Animation.create (fun t ->
                        let angle = t |> lerp 0.0 angleInRadians
                        Rot3d.Rotation(normalizedAxis, angle)
                    )

                (center, rotation)
                ||> Animation.input (fun center rotation ->
                    let location = center + rotation * (camera.Location - center)
                    CameraView.lookAt location center camera.Sky
                )

            /// Creates an animation that orbits the camera view variable specified by
            /// the lens around the center and axis by the given angle. The animation is linked to that variable via an observer with a progress callback.
            let orbitTo' (lens : Lens<'Model, CameraView>) (center : IAnimation<'Model, V3d>) (normalizedAxis : V3d) (angleInRadians : float) (model : 'Model) =
                orbit' center normalizedAxis angleInRadians (model |> Optic.get lens)
                |> Animation.link lens

            /// Creates an animation that orbits the camera view around the center and axis by the given angle.
            let orbitDynamic (center : Lens<'Model, V3d>) (normalizedAxis : V3d) (angleInRadians : float) (camera : CameraView) : IAnimation<'Model, CameraView> =
                Animation.create' (fun m t ->
                    let angle = t |> lerp 0.0 angleInRadians
                    let rotation = Rot3d.Rotation(normalizedAxis, angle)
                    let center = m |> Optic.get center
                    let location = center + rotation * (camera.Location - center)
                    CameraView.lookAt location center camera.Sky
                )

            /// Creates an animation that orbits the camera view variable specified by
            /// the lens around the center and axis by the given angle. The animation is linked to that variable via an observer with a progress callback.
            let orbitDynamicTo (lens : Lens<'Model, CameraView>) (center : Lens<'Model, V3d>) (normalizedAxis : V3d) (angleInRadians : float) (model : 'Model) =
                orbitDynamic center normalizedAxis angleInRadians (model |> Optic.get lens)
                |> Animation.link lens

            /// <summary>
            /// Creates an array of animations that interpolate linearly between pairs of the given camera views.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let linearPath' (points : CameraView seq) : IAnimation<'Model, CameraView>[] =

                let points = Array.ofSeq points

                if Array.isEmpty points then
                    raise <| System.ArgumentException("Camera path cannot be empty.")

                let sky = points.[0].Sky
                let positions = points |> Array.map CameraView.location
                let orientations = points |> Array.map CameraView.orientation

                let interp (p : V3d, o : Rot3d) (p' : V3d, o' : Rot3d) =
                    (Primitives.lerp p p', Primitives.slerp o o')
                    ||> Animation.map2 (fun l o -> l, o)

                let dist (x : V3d, _) (y : V3d, _) =
                    Vec.distance x y

                Array.zip positions orientations
                |> Primitives.path' interp dist
                |> Array.map (Animation.map (fun (l, o) -> CameraView.orient l o sky))

            /// <summary>
            /// Creates an animation that interpolates linearly between the given camera views.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let linearPath (points : CameraView seq) : IAnimation<'Model, CameraView> =
                points |> linearPath' |> Animation.sequential

            /// <summary>
            /// Creates an array of animations that smoothly interpolate between the given camera views.
            /// The animations are scaled according to the distance between the camera view locations.
            /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
            /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let smoothPath' (errorTolerance : float) (points : CameraView seq) : IAnimation<'Model, CameraView>[] =
                let points = Array.ofSeq points

                if Array.isEmpty points then
                    raise <| System.ArgumentException("Camera path cannot be empty.")

                let sky = points.[0].Sky

                let locations =
                    points
                    |> Array.map CameraView.location
                    |> Primitives.smoothPath' Vec.distance errorTolerance

                let orientations =
                    points
                    |> Array.map CameraView.orientation
                    |> Primitives.path' Primitives.slerp (fun _ _ -> 1.0)

                (locations, orientations)
                ||> Array.map2 (fun l o ->
                    let o = o |> Animation.duration l.Duration
                    (l, o) ||> Animation.map2 (fun l o -> CameraView.orient l o sky)
                )

            /// <summary>
            /// Creates an animation that smoothly interpolates between the given camera views.
            /// The accuracy of the parameterization depends on the given error tolerance, a value in the range of [Splines.MinErrorTolerance, 1].
            /// Reducing the error tolerance increases both accuracy and memory usage; use Splines.DefaultErrorTolerance for a good balance.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let smoothPath (errorTolerance : float) (points : CameraView seq) : IAnimation<'Model, CameraView> =
                points |> smoothPath' errorTolerance |> Animation.sequential