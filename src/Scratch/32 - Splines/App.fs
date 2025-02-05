module SplinesTest.App

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Animation
open FSharp.Data.Adaptive
open SplinesTest.Model

[<AutoOpen>]
module PointShaders =
    open FShade

    type UniformScope with
        member x.PointSize : float = uniform?PointSize

    module Semantic =
        let PointSize = Sym.ofString "PointSize"

    module Shader =
        type PointVertex =
            {
                [<Position>] pos : V4d
                [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc : V2d
            }

        let pointTransform (v: PointVertex) =
            vertex {
                return V4d(v.pos.XY * 2.0 - 1.0, 0.0, 1.0)
            }

let camera =
    let view = CameraView.look V3d.Zero V3d.YAxis V3d.ZAxis
    let frustum = Frustum.perspective 90.0 0.1 100.0 1.0
    AVal.constant <| Camera.create view frustum

let addTolerance (delta: float) (model: Model) =
    { model with ErrorTolerance = clamp Splines.MinErrorTolerance 0.2 (model.ErrorTolerance + delta) }

let update (model: Model) (msg: Message) =
    match msg with
    | Add p ->
        { model with Points = Array.append model.Points [| V2d(p.X, 1.0 - p.Y) |] }

    | OnKeyDown Keys.Delete ->
        { model with Points = [||] }

    | OnKeyDown Keys.Back ->
        let n = max 0 (model.Points.Length - 1)
        { model with Points = model.Points |> Array.take n}

    | OnKeyDown Keys.OemPlus ->
        model |> addTolerance 0.001

    | OnKeyDown Keys.OemMinus ->
        model |> addTolerance -0.001

    | OnWheel d->
        model |> addTolerance (0.0001 * signum d.Y)

    | _ ->
        model

let view (model: AdaptiveModel) =
    let p0 = RenderPass.main
    let p1 = p0 |> RenderPass.after "p1" RenderPassOrder.Arbitrary
    let p2 = p1 |> RenderPass.after "p1" RenderPassOrder.Arbitrary

    let renderPoints (pass: RenderPass) (color: C4f) (size: float) (points: aval<V2d[]>) =
        let points =
            points |> AVal.map(Seq.map v3f >> Seq.toArray)

        Sg.draw IndexedGeometryMode.PointList
        |> Sg.vertexAttribute DefaultSemantic.Positions points
        |> Sg.uniform' Semantic.PointSize size
        |> Sg.shader {
            do! Shader.pointTransform
            do! DefaultSurfaces.pointSprite
            do! DefaultSurfaces.pointSpriteFragment
            do! DefaultSurfaces.constantColor color
        }
        |> Sg.pass pass

    let sg =
        let splinePoints =
            model.Splines |> AVal.map (fun s ->
                s |> Array.collect (fun s ->
                    let n = int <| s.Length * 512.0
                    Array.init n (fun i ->
                        s.Evaluate <| float i / float (n - 1)
                    )
                )
            )
            |> renderPoints p0 C4f.DarkRed 4.0

        let controlPoints = model.Points |> renderPoints p1 C4f.AliceBlue 12.0
        let samplePoints = model.Samples |> renderPoints p2 C4f.VRVisGreen 8.0

        Sg.ofList [ controlPoints; samplePoints; splinePoints ]

    body [
        style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden"
    ] [
        renderControl camera [
             style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden"
             onMouseClickRel (fun _ p -> Add p)
             onKeyDown OnKeyDown
             onWheel OnWheel
        ] sg

        div [style "position: absolute; left: 10px; top: 10px; color: white; pointer-events: none; font-family: monospace; font-size: larger;"] [
            model.ErrorTolerance
            |> AVal.map (fun t -> $"Error tolerance: %.5f{t}")
            |> Incremental.text
        ]
    ]

let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance
        threads = fun _ -> ThreadPool.empty
        initial =
            {
               Points = [||]
               ErrorTolerance = 0.01
            }
        update = update
        view = view
    }