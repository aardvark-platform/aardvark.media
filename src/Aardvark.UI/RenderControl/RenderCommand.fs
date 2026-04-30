namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

type internal RCmd = RenderCommand

[<Struct>]
type RenderCommand<'msg>(command: RCmd) =
    static let box (sg: ISg<'msg>) = sg :> ISg

    member _.Command = command
    static member op_Implicit (cmd: RenderCommand<'msg>) = cmd.Command

    static member Empty =
        RenderCommand<'msg> RCmd.Empty

    static member Clear(values : aval<ClearValues>) =
        RenderCommand<'msg> <| RCmd.Clear values

    static member Clear(values : ClearValues) =
        RenderCommand<'msg>.Clear(~~values)

    static member inline Clear(color : aval< ^Color>) =
        let values = color |> AVal.map (fun c -> clear { color c })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(color : aval< ^Color>, depth : aval< ^Depth>) =
        let values = (color, depth) ||> AVal.map2 (fun c d -> clear { color c; depth d })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(color : aval< ^Color>, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
        let values = (color, depth, stencil) |||> AVal.map3 (fun c d s -> clear { color c; depth d; stencil s })
        RenderCommand<'msg>.Clear(values)

    static member inline ClearDepth(depth : aval< ^Depth>) =
        let values = depth |> AVal.map (fun d -> clear { depth d })
        RenderCommand<'msg>.Clear(values)

    static member inline ClearStencil(stencil : aval< ^Stencil>) =
        let values = stencil |> AVal.map (fun s -> clear { stencil s })
        RenderCommand<'msg>.Clear(values)

    static member inline ClearDepthStencil(depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
        let values = (depth, stencil) ||> AVal.map2 (fun d s -> clear { depth d; stencil s })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(color : ^Color, depth : ^Depth)                     = RenderCommand<'msg>.Clear(~~color, ~~depth)
    static member inline Clear(color : ^Color, depth : ^Depth, stencil : ^Stencil) = RenderCommand<'msg>.Clear(~~color, ~~depth, ~~stencil)
    static member inline ClearDepth(depth : ^Depth)                                = RenderCommand<'msg>.ClearDepth(~~depth)
    static member inline ClearStencil(stencil : ^Stencil)                          = RenderCommand<'msg>.ClearStencil(~~stencil)
    static member inline ClearDepthStencil(depth : ^Depth, stencil : ^Stencil)     = RenderCommand<'msg>.ClearDepthStencil(~~depth, ~~stencil)

    static member inline Clear(colors : Map<Symbol, ^Color>) =
        let values = colors |> (fun c -> clear { colors c })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(colors : seq<Symbol * ^Color>) =
        let values = colors |> (fun c -> clear { colors c })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(colors : Map<Symbol, ^Color>, depth : ^Depth) =
        let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(colors : seq<Symbol * ^Color>, depth : ^Depth) =
        let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(colors : Map<Symbol, ^Color>, depth : ^Depth, stencil : ^Stencil) =
        let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
        RenderCommand<'msg>.Clear(values)

    static member inline Clear(colors : seq<Symbol * ^Color>, depth : ^Depth, stencil : ^Stencil) =
        let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
        RenderCommand<'msg>.Clear(values)

    static member Unordered(scenes : seq<ISg<'msg>>) =
        if Seq.isEmpty scenes then RenderCommand<'msg>.Empty
        else RenderCommand<'msg> <| RCmd.Unordered(scenes |> Seq.map box)

    static member Unordered(scenes : list<ISg<'msg>>) =
        match scenes with
        | [] -> RenderCommand<'msg>.Empty
        | _  -> RenderCommand<'msg> <| RCmd.Unordered(scenes |> List.map box)

    static member Unordered(scenes : aset<ISg<'msg>>) =
        if scenes.IsConstant && scenes.Content.GetValue().IsEmpty then RenderCommand<'msg>.Empty
        else RenderCommand<'msg> <| RCmd.Unordered(scenes |> ASet.map box)

    static member Render(scene : ISg<'msg>) =
        RenderCommand<'msg> <| RCmd.Render scene

    static member Ordered(commands : seq<RenderCommand<'msg>>) =
        if Seq.isEmpty commands then RenderCommand<'msg>.Empty
        else RenderCommand<'msg> <| RCmd.Ordered(commands |> Seq.map _.Command)

    static member Ordered(commands : list<RenderCommand<'msg>>) =
        match commands with
        | [] -> RenderCommand<'msg>.Empty
        | _ -> RenderCommand<'msg> <| RCmd.Ordered(commands |> List.map _.Command)

    static member Ordered(commands : alist<RenderCommand<'msg>>) =
        if commands.IsConstant && commands.Content.GetValue().Count = 0 then RenderCommand<'msg>.Empty
        else RenderCommand<'msg> <| RCmd.Ordered(commands |> AList.map _.Command)

    static member Ordered(scenes : seq<ISg<'msg>>) =
        RenderCommand<'msg> <| RCmd.Ordered(scenes |> Seq.map RCmd.Render)

    static member Ordered(scenes : list<ISg<'msg>>) =
        RenderCommand<'msg> <| RCmd.Ordered(scenes |> List.map RCmd.Render)

    static member Ordered(scenes : alist<ISg<'msg>>)  =
        RenderCommand<'msg> <| RCmd.Ordered(scenes |> AList.map RCmd.Render)

    static member IfThenElse(condition : aval<bool>, ifTrue : RenderCommand<'msg>, ifFalse : RenderCommand<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, ifTrue.Command, ifFalse.Command)

    static member IfThenElse(condition : aval<bool>, ifTrue : ISg<'msg>, ifFalse : ISg<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, RCmd.Render ifTrue, RCmd.Render ifFalse)

    static member When(condition : aval<bool>, ifTrue : RenderCommand<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, ifTrue.Command, RCmd.Empty)

    static member When(condition : aval<bool>, ifTrue : ISg<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, RCmd.Render ifTrue, RCmd.Empty)

    static member WhenNot(condition : aval<bool>, ifFalse : RenderCommand<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, RCmd.Empty, ifFalse.Command)

    static member WhenNot(condition : aval<bool>, ifFalse : ISg<'msg>) =
        RenderCommand<'msg> <| RCmd.IfThenElse(condition, RCmd.Empty, RCmd.Render ifFalse)

    static member LodTree(config : RenderGeometryConfig, geometries : LodTreeLoader<Geometry>) =
        RenderCommand<'msg> <| RCmd.LodTree(config, geometries)

    static member Geometries(config : RenderGeometryConfig,  geometries : aset<IndexedGeometry>) =
        RenderCommand<'msg> <| RCmd.Geometries(config, geometries)

    static member Geometries(config : RenderGeometryConfig,  geometries : seq<IndexedGeometry>) =
        RenderCommand<'msg> <| RCmd.Geometries(config, ASet.ofSeq geometries)

    static member Geometries(config : RenderGeometryConfig,  geometries : list<IndexedGeometry>) =
        RenderCommand<'msg> <| RCmd.Geometries(config, ASet.ofList geometries)

[<AutoOpen>]
module ``Sg RuntimeCommand Extensions`` =

    module Sg =

        /// Executes the given render command.
        let execute (cmd : RenderCommand<'msg>) : ISg<'msg> =
            Sg.RuntimeCommandNode(cmd.Command) |> Sg.noEvents