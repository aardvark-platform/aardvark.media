namespace DrawRects

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

module RenderQuads =
    let size = V2i(256,256)

    let encode (bytes : byte[]) =
      let header = "data:image/"
      System.Convert.ToBase64String(bytes);

    let renderQuad (colors : C4f[]) (runtime : IRuntime) =
        let signature = 
            runtime.CreateFramebufferSignature [ DefaultSemantic.Colors, RenderbufferFormat.Rgba8 ]
        let rt = 
             Aardvark.SceneGraph.SgPrimitives.Sg.fullScreenQuad
             |> Sg.vertexAttribute DefaultSemantic.Colors (AVal.constant colors)
             |> Sg.shader {
                 do! DefaultSurfaces.vertexColor
                }
             |> Sg.compile runtime signature
             |> RenderTask.renderToColor (AVal.constant size)
        let t = rt.GetValue()
        let t2 = runtime.Download(t |> unbox)
        encode (unbox t2.Data), size