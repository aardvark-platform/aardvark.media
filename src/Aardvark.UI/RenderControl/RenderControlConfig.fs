namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Rendering

type RenderControlConfig =
    {
        adjustAspect : V2i -> Frustum -> Frustum
    }

module RenderControlConfig =

    /// Adjusts the viewport aspect by scaling vertical bounds, preserving horizontal bounds.
    let fillHeight =
        {
            adjustAspect = fun size -> Frustum.withAspect (float size.X / float size.Y)
        }

    /// Adjusts the viewport aspect by scaling horizontal bounds, preserving vertical bounds.
    let fillWidth =
        {
            adjustAspect = fun size ({ left = l; right = r; top = t; bottom = b } as f) ->
                let newAspect = float size.Y / float size.X
                let factor = ((t - b) / (r - l)) / newAspect
                { f with right = factor * r; left = factor * l }
        }

    let noScaling =
        {
            adjustAspect = fun _ frustum -> frustum
        }

    /// <summary>
    /// Adjusts the viewport aspect by scaling vertical bounds, preserving horizontal bounds.
    /// </summary>
    /// <remarks>
    /// Equivalent to <see cref="fillHeight"/>.
    /// </remarks>
    let standard = fillHeight