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
            member x.LightViewProj : M44f = uniform?LightViewProj
            member x.LightDirection : V3f = uniform?LightDirection

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
                 V2f( -0.94201624f,  -0.39906216f )
                 V2f(  0.94558609f,  -0.76890725f )
                 V2f( -0.094184101f, -0.92938870f )
                 V2f(  0.34495938f,   0.29387760f )
                 V2f( -0.91588581f,   0.45771432f )
                 V2f( -0.81544232f,  -0.87912464f )
                 V2f( -0.38277543f,   0.27676845f )
                 V2f(  0.97484398f,   0.75648379f )
                 V2f(  0.44323325f,  -0.97511554f )
                 V2f(  0.53742981f,  -0.47373420f )
                 V2f( -0.26496911f,  -0.41893023f )
                 V2f(  0.79197514f,   0.19090188f )
                 V2f( -0.24188840f,   0.99706507f )
                 V2f( -0.81409955f,   0.91437590f )
                 V2f(  0.19984126f,   0.78641367f )
                 V2f(  0.14383161f,  -0.14100790f )
             |]

        [<ReflectedDefinition>]
        let private getShadow (wp : V4f) =
            let lightSpace = uniform.LightViewProj * wp
            let div = lightSpace.XYZ / lightSpace.W
            let tc = V3f.Half + V3f.Half * div.XYZ

            // PCF using offset disk from
            // http://developer.download.nvidia.com/whitepapers/2008/PCSS_Integration.pdf
            let mutable sum = 0.0f
            for i = 0 to 15 do
                let offset = poissonDisk.[i] * (1.0f / 4096.0f)
                sum <- sum + shadowSampler.Sample(tc.XY + offset, tc.Z - 0.01f)

            0.1f + sum / 16.0f

        let lighting (v : Vertex) =
            fragment {
                let n = v.n |> Vec.normalize
                let l = uniform.LightDirection |> Vec.normalize

                let ambient = 0.1f
                let NdotL = Vec.dot n l
                let diffuse =
                    if NdotL > 0.0f then
                        NdotL * getShadow v.wp
                    else
                        0.0f

                return V4f(v.c.XYZ * diffuse + ambient, v.c.W)
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