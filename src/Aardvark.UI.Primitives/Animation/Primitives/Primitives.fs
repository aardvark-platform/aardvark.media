namespace Aardvark.UI.Animation

open Aardvark.Base

[<AutoOpen>]
module AnimationPrimitives =

    module Animation =

        module Primitives =

            module Utilities =

                type DoubleConverter =
                    static member inline ToDouble(x : float) = x
                    static member inline ToDouble(x : float32) = float x
                    static member inline OfDouble(x : float, _ : float) = x
                    static member inline OfDouble(x : float, _ : float32) = float32 x

                    static member inline ToRot3d(x : Rot3d) = x
                    static member inline ToRot3d(x : Rot3f) = Rot3d x
                    static member inline OfRot3d(x : Rot3d, _ : Rot3d) = x
                    static member inline OfRot3d(x : Rot3d, _ : Rot3f) = Rot3f x

                [<AutoOpen>]
                module internal Aux =
                    let inline toDoubleAux (_ : ^z) (x : ^Value) =
                        ((^z or ^Value) : (static member ToDouble : ^Value -> float) x)

                    let inline ofDoubleAux (_ : ^z) (x : float) : ^Value =
                        ((^z or ^Value) : (static member OfDouble : float * ^Value -> ^Value) (x, Unchecked.defaultof< ^Value>))

                    let inline toRot3dAux (_ : ^z) (x : ^Value) =
                        ((^z or ^Value) : (static member ToRot3d : ^Value -> Rot3d) x)

                    let inline ofRot3dAux (_ : ^z) (x : Rot3d) : ^Value =
                        ((^z or ^Value) : (static member OfRot3d : Rot3d * ^Value -> ^Value) (x, Unchecked.defaultof< ^Value>))

                    let inline toDouble (x : ^Value) =
                        toDoubleAux Unchecked.defaultof<DoubleConverter> x

                    let inline ofDouble (x : float) : ^Value =
                        ofDoubleAux Unchecked.defaultof<DoubleConverter> x

                    let inline toRot3d (x : ^Value) =
                        toRot3dAux Unchecked.defaultof<DoubleConverter> x

                    let inline ofRot3d (x : Rot3d) : ^Value =
                        ofRot3dAux Unchecked.defaultof<DoubleConverter> x

            open Aether
            open Utilities

            /// Creates an animation that returns a constant value.
            let constant (value : 'Value) : IAnimation<'Model, 'Value> =
                Animation.create (fun _ -> value)


            /// Creates an animation that linearly interpolates between src and dst.
            let inline lerp (src : ^Value) (dst : ^Value) : IAnimation<'Model, ^Value> =
                Animation.create (lerp src dst)
                |> Animation.seconds 1

            /// Creates an animation that linearly interpolates the variable specified by
            /// the lens to dst. The animation is linked to that variable via an observer with a progress callback.
            let inline lerpTo (lens : Lens<'Model, ^Value>) (dst : ^Value) (model : 'Model) =
                lerp (model |> Optic.get lens) dst
                |> Animation.link lens


            /// Creates an animation that linearly interpolates between the given angles in radians.
            /// The value type can be either float or float32.
            let inline lerpAngle (srcInRadians : ^Value) (dstInRadians : ^Value) : IAnimation<'Model, ^Value> =
                let src, dst = toDouble srcInRadians, toDouble dstInRadians
                let diff = Fun.AngleDifference(src, dst)

                lerp src (src + diff)
                |> Animation.map (fun value -> ofDouble <| value % Constant.PiTimesTwo)

            /// Creates an animation that linearly interpolates the variable specified by
            /// the lens to the given angle in radians. The animation is linked to that variable via an observer with a progress callback.
            /// The value type can be either float or float32.
            let inline lerpAngleTo (lens : Lens<'Model, ^Value>) (dstInRadians : ^Value) (model : 'Model) =
                lerpAngle (model |> Optic.get lens) dstInRadians
                |> Animation.link lens


            /// Creates an animation using spherical linear interpolation between the given orientations.
            /// The orientation type can be either Rot3d or Rot3f.
            let inline slerp (src : ^Rot3) (dst : ^Rot3) : IAnimation<'Model, ^Rot3> =
                let src, dst = toRot3d src, toRot3d dst

                Animation.create (fun t -> Rot.SlerpShortest(src, dst, t))
                |> Animation.seconds 1
                |> Animation.map (ofRot3d)

            /// Creates an animation using spherical linear interpolation.
            /// The animation is linked to the variable specified by the given lens via an observer with a progress callback.
            /// The orientation type can be either Rot3d or Rot3f.
            let inline slerpTo (lens : Lens<'Model, ^Rot3>) (dst : ^Rot3) (model : 'Model) =
                slerp (model |> Optic.get lens) dst
                |> Animation.link lens


            /// Creates an array of animations that interpolate between pairs of the given points.
            /// The animations are scaled according to the distance between the points. Coinciding points are ignored.
            let inline path' (interpolate : ^Value -> ^Value -> IAnimation<'Model, ^Value>)
                             (distance : ^Value -> ^Value -> float)
                             (points : ^Value seq) : IAnimation<'Model, ^Value>[] =

                let pj = Array.ofSeq points
                let dj = Array.zeroCreate pj.Length

                // Filter duplicates and create animations
                if Array.isEmpty pj then
                    [||]
                else
                    let mutable n = 1

                    for i = 1 to pj.Length - 1 do
                        let d = distance pj.[n - 1] pj.[i]
                        if d.ApproximateEquals 0.0 then
                            Log.warn "[Animation] Ignoring duplicate control point in path"
                        else
                            pj.[n] <- pj.[i]
                            dj.[n] <- d
                            inc &n

                    if n = 1 then
                        [| constant pj.[0] |]
                    else
                        let maxLength = dj |> Array.max

                        let segments = Array.zeroCreate (n - 1)
                        for i = 0 to (n - 2) do
                            let d = dj.[i + 1] / maxLength
                            segments.[i] <- interpolate pj.[i] pj.[i + 1] |> Animation.seconds d

                        segments

            /// <summary>
            /// Creates an animation that interpolates between the given points. Coinciding points are ignored.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let inline path (interpolate : ^Value -> ^Value -> IAnimation<'Model, ^Value>)
                            (distance : ^Value -> ^Value -> float)
                            (points : ^Value seq) : IAnimation<'Model, ^Value> =

                points |> path' interpolate distance |> Animation.path


            /// Creates an array of animations that interpolate linearly between pairs of the given points.
            /// The animations are scaled according to the distance between the points. Coinciding points are ignored.
            let inline linearPath' (distance : ^Value -> ^Value -> float) (points : ^Value seq) : IAnimation<'Model, ^Value>[] =
                points |> path' lerp distance

            /// <summary>
            /// Creates an animation that linearly interpolates between the given points. Coinciding points are ignored.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if the sequence is empty.</exception>
            let inline linearPath (distance : ^Value -> ^Value -> float) (points : ^Value seq) : IAnimation<'Model, ^Value> =
                points |> path lerp distance