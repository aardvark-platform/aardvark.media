namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Rendering

type RenderControlConfig =
    {
        adjustAspect : V2i -> Frustum -> Frustum
    }

module RenderControlConfig =

    /// Fills height, depending on aspect ratio.
    let standard =
        {
            adjustAspect = fun size -> Frustum.withAspect (float size.X / float size.Y)
        }

    /// Fills height, depending on aspect ratio.
    let fillHeight = standard

    /// Fills width, depending on aspect ratio-
    let fillWidth =
        let aspect { left = l; right = r; top = t; bottom = b } =
            (t - b) / (r - l)

        let withAspectFlipped (newAspect : float) ( { left = _; right = _; top = t; bottom = b } as f)  =
            let factor = 1.0 - (newAspect / aspect f)
            { f with bottom = factor * t + b; top  = factor * b + t }

        {
            adjustAspect = fun size -> withAspectFlipped (float size.X / float size.Y)
        }

    let noScaling =
        {
            adjustAspect = fun _ frustum -> frustum
        }