namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.UI
open FSharp.Data.Adaptive

[<AutoOpen>]
module ShadowMappingShaders =

    module Shader =
        open FShade
        open Aardvark.Rendering.Effects

        type UniformScope with
            member x.LightViewProj : M44d = uniform?LightViewProj
            member x.LightDirection : V3d = uniform?LightDirection

        let private shadowSampler =
            sampler2dShadow {
                texture uniform?ShadowTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Border
                addressV WrapMode.Border
                borderColor C4f.White
                comparison ComparisonFunction.LessOrEqual
            }

        let private poissonDisk =
             [|
                 V2d( -0.94201624,  -0.39906216 )
                 V2d(  0.94558609,  -0.76890725 )
                 V2d( -0.094184101, -0.92938870 )
                 V2d(  0.34495938,   0.29387760 )
                 V2d( -0.91588581,   0.45771432 )
                 V2d( -0.81544232,  -0.87912464 )
                 V2d( -0.38277543,   0.27676845 )
                 V2d(  0.97484398,   0.75648379 )
                 V2d(  0.44323325,  -0.97511554 )
                 V2d(  0.53742981,  -0.47373420 )
                 V2d( -0.26496911,  -0.41893023 )
                 V2d(  0.79197514,   0.19090188 )
                 V2d( -0.24188840,   0.99706507 )
                 V2d( -0.81409955,   0.91437590 )
                 V2d(  0.19984126,   0.78641367 )
                 V2d(  0.14383161,  -0.14100790 )
             |]

        [<ReflectedDefinition>]
        let private getShadow (wp : V4d) =
            let lightSpace = uniform.LightViewProj * wp
            let div = lightSpace.XYZ / lightSpace.W
            let tc = V3d.Half + V3d.Half * div.XYZ

            // PCF using offset disk from
            // http://developer.download.nvidia.com/whitepapers/2008/PCSS_Integration.pdf
            let mutable sum = 0.0
            for i = 0 to 15 do
                let offset = poissonDisk.[i] * (1.0 / 4096.0)
                sum <- sum + shadowSampler.Sample(tc.XY + offset, tc.Z - 0.01)

            0.1 + sum / 16.0

        let lighting (v : Vertex) =
            fragment {
                let n = v.n |> Vec.normalize
                let l = uniform.LightDirection |> Vec.normalize

                let ambient = 0.1
                let NdotL = Vec.dot n l
                let diffuse =
                    if NdotL > 0.0 then
                        NdotL * getShadow v.wp
                    else
                        0.0

                return V4d(v.c.XYZ * diffuse + ambient, v.c.W)
            }

module Shadows =

    let lightProj =
        Frustum.ortho (Box3d(-20.0, -20.0, 0.1, 20.0, 20.0, 80.0)) |> Frustum.projTrafo

    let lightView (lightDir : aval<V3d>) =
        lightDir |> AVal.map (fun dir ->
            CameraView.look (dir.Normalized * 10.0) -dir V3d.ZAxis |> CameraView.viewTrafo
        )

    let lightViewProj (lightDir : aval<V3d>) =
        lightDir |> lightView |> AVal.map (fun view -> view * lightProj)

    let computeShadowMap (signature : IFramebufferSignature) (lightDir : aval<V3d>) (sg : ISg<'Msg>) =
        let runtime = signature.Runtime :?> IRuntime
        let shadowMapSize = V2i(4096, 4096) |> AVal.constant

        sg
        |> Sg.viewTrafo (lightView lightDir)
        |> Sg.projTrafo' lightProj
        |> Sg.compile runtime signature
        |> RenderTask.renderToDepth shadowMapSize