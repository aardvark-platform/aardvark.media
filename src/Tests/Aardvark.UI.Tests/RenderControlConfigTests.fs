namespace Aardvark.UI.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Expecto

module ``RenderControlConfig Tests`` =

    let private viewports =
        [
            "landscape", V2i(800, 400)
            "portrait", V2i(400, 800)
            "square", V2i(600, 600)
            "already matching", V2i(900, 600)
        ]

    let private frusta =
        [
            for isOrtho in [false; true] do
                let projection = if isOrtho then "orthographic" else "perspective"
                let centered =
                    { left = -3.0; right = 3.0; bottom = -2.0; top = 2.0
                      near = 0.5; far = 125.0; isOrtho = isOrtho }
                yield $"centered {projection}", centered
                yield $"off-center {projection}",
                    { centered with left = -1.0; right = 5.0; bottom = -0.5; top = 3.5 }
        ]

    let private checkGeometry context preserveHorizontal (size : V2i) (original : Frustum) (adjusted : Frustum) =
        Expect.equal adjusted.near original.near $"{context}: near plane"
        Expect.equal adjusted.far original.far $"{context}: far plane"
        Expect.equal adjusted.isOrtho original.isOrtho $"{context}: projection type"
        let width = adjusted.right - adjusted.left
        let height = adjusted.top - adjusted.bottom
        let viewportAspect = float size.X / float size.Y
        if preserveHorizontal then
            Expect.equal adjusted.left original.left $"{context}: left bound"
            Expect.equal adjusted.right original.right $"{context}: right bound"
            Expect.floatClose Accuracy.high height ((original.right - original.left) / viewportAspect)
                $"{context}: height follows preserved width"
        else
            Expect.equal adjusted.top original.top $"{context}: top bound"
            Expect.equal adjusted.bottom original.bottom $"{context}: bottom bound"
            Expect.floatClose Accuracy.high width ((original.top - original.bottom) * viewportAspect)
                $"{context}: width follows preserved height"
        Expect.floatClose Accuracy.high (width / height) viewportAspect $"{context}: viewport aspect"
        let normalizedOffset (f : Frustum) =
            if preserveHorizontal then (f.top + f.bottom) / (2.0 * (f.top - f.bottom))
            else (f.right + f.left) / (2.0 * (f.right - f.left))
        Expect.floatClose Accuracy.high (normalizedOffset adjusted) (normalizedOffset original)
            $"{context}: normalized offset on scaled axis"
        if (original.right - original.left) / (original.top - original.bottom) = viewportAspect then
            Expect.equal adjusted original $"{context}: matching aspect needs no adjustment"

    let private checkPixelScales context preserveHorizontal (size : V2i) (original : Frustum) (adjusted : Frustum) =
        // Project unit displacements at one depth, then convert NDC distances to pixels.
        let depth = 2.0 * original.near
        let origin = V3d(0.0, 0.0, -depth)
        let projection = (Frustum.projTrafo adjusted).Forward
        let projectedOrigin = projection.TransformPosProj origin
        let horizontal = projection.TransformPosProj (origin + V3d.XAxis) - projectedOrigin
        let vertical = projection.TransformPosProj (origin + V3d.YAxis) - projectedOrigin
        let horizontalPixels = abs horizontal.X * float size.X / 2.0
        let verticalPixels = abs vertical.Y * float size.Y / 2.0
        let depthScale = if original.isOrtho then 1.0 else original.near / depth
        let preservedPixels, expected =
            if preserveHorizontal then
                horizontalPixels, float size.X * depthScale / (original.right - original.left)
            else
                verticalPixels, float size.Y * depthScale / (original.top - original.bottom)
        Expect.floatClose Accuracy.high preservedPixels expected $"{context}: preserved pixel scale"
        Expect.floatClose Accuracy.high horizontalPixels verticalPixels $"{context}: equal pixel scales"

    [<Tests>]
    let tests =
        testList "RenderControlConfig Tests" [
            test "fillWidth matches the viewport while preserving vertical bounds and horizontal offset" {
                for frustumName, frustum in frusta do
                    for viewportName, size in viewports do
                        let context = $"{frustumName}, {viewportName}"
                        checkGeometry context false size frustum (RenderControlConfig.fillWidth.adjustAspect size frustum)
            }

            test "fillWidth retains vertical pixel scale with equal horizontal and vertical scales" {
                for frustumName, frustum in frusta do
                    for viewportName, size in viewports do
                        let context = $"{frustumName}, {viewportName}"
                        checkPixelScales context false size frustum (RenderControlConfig.fillWidth.adjustAspect size frustum)
            }

            test "standard and fillHeight retain their aspect adjustment" {
                for configName, config in ["standard", RenderControlConfig.standard; "fillHeight", RenderControlConfig.fillHeight] do
                    for frustumName, frustum in frusta do
                        for viewportName, size in viewports do
                            let context = $"{configName}, {frustumName}, {viewportName}"
                            let adjusted = config.adjustAspect size frustum
                            checkGeometry context true size frustum adjusted
                            checkPixelScales context true size frustum adjusted
            }

            test "noScaling preserves every frustum regardless of viewport" {
                for frustumName, frustum in frusta do
                    for viewportName, size in viewports do
                        Expect.equal (RenderControlConfig.noScaling.adjustAspect size frustum) frustum
                            $"{frustumName}, {viewportName}: no adjustment"
            }
        ]
