namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

type RenderCommand<'msg> =
    | Clear      of color: aval<C4f> option * depth: aval<float> option * stencil: aval<int> option
    | SceneGraph of sg: ISg<'msg>

    member x.Compile(values : RenderClientValues) =
        match x with
        | RenderCommand.Clear(color, depth, stencil) ->
            match color, depth, stencil with
            | Some c, Some d, Some s -> values.runtime.CompileClear(values.signature, c, d, s)
            | None, Some d, Some s   -> values.runtime.CompileClearDepthStencil(values.signature, d, s)
            | Some c, None, Some s   -> values.runtime.CompileClear(values.signature, (c, s) ||> AVal.map2 (fun c s -> clear { color c; stencil s }))
            | None, None, Some s     -> values.runtime.CompileClearStencil(values.signature, s)
            | Some c, Some d, None   -> values.runtime.CompileClear(values.signature, c, d)
            | None, Some d, None     -> values.runtime.CompileClearDepth(values.signature, d)
            | Some c, None, None     -> values.runtime.CompileClear(values.signature, c)
            | None, None, None       -> RenderTask.empty

        | RenderCommand.SceneGraph sg ->
            let sg =
                sg
                |> Sg.viewTrafo values.viewTrafo
                |> Sg.projTrafo values.projTrafo
                |> Sg.uniform "ViewportSize" values.size
            values.runtime.CompileRender(values.signature, sg)
