﻿namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<AutoOpen>]
module AnimationPrimitives =

    module Animation =

        open Aether

        /// Creates an animation that linearly interpolates between src and dst.
        let inline lerp (src : ^Value) (dst : ^Value) =
            let sf = fun s -> s |> lerp src dst

            Animation.empty
            |> Animation.spaceFunction sf
            |> Animation.seconds 1

        /// Creates an animation that linearly interpolates the variable specified by
        /// the lens to dst. The animation is linked to that variable.
        let inline lerpTo (lens : Lens<'Model, ^Value>) (dst : ^Value) (model : 'Model) =
            lerp (model |> Optic.get lens) dst
            |> Animation.link lens