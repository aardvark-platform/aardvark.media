namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<AutoOpen>]
module AnimationPrimitives =

    module Animation =

        module PrimitiveUtilities =

            type DoubleConverter =
                static member inline ToDouble(x : float) = x
                static member inline ToDouble(x : float32) = float x
                static member inline OfDouble(x : float, _ : float) = x
                static member inline OfDouble(x : float, _ : float32) = float32 x

            [<AutoOpen>]
            module internal Aux =
                let inline toDoubleAux (_ : ^z) (x : ^Value) =
                    ((^z or ^Value) : (static member ToDouble : ^Value -> float) x)

                let inline ofDoubleAux (_ : ^z) (x : float) : ^Value =
                    ((^z or ^Value) : (static member OfDouble : float * ^Value -> ^Value) (x, Unchecked.defaultof< ^Value>))

                let inline toDouble (x : ^Value) =
                    toDoubleAux Unchecked.defaultof<DoubleConverter> x

                let inline ofDouble (x : float) : ^Value =
                    ofDoubleAux Unchecked.defaultof<DoubleConverter> x

        open Aether
        open PrimitiveUtilities

        /// Creates an animation that returns a constant value.
        let constant (value : 'Value) : IAnimation<'Model, 'Value> =
            Animation.create (fun _ -> value)

        /// Creates an animation that linearly interpolates between src and dst.
        let inline lerp (src : ^Value) (dst : ^Value) : IAnimation<'Model, ^Value> =
            Animation.create (lerp src dst)
            |> Animation.seconds 1

        /// Creates an animation that linearly interpolates the variable specified by
        /// the lens to dst. The animation is linked to that variable.
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
        /// the lens to the given angle in radians. The animation is linked to that variable.
        /// The value type can be either float or float32.
        let inline lerpAngleTo (lens : Lens<'Model, ^Value>) (dstInRadians : ^Value) (model : 'Model) =
            lerpAngle (model |> Optic.get lens) dstInRadians
            |> Animation.link lens