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
        member x.PointSize : float32 = uniform?PointSize

    module Semantic =
        let PointSize = Sym.ofString "PointSize"

    module Shader =
        type PointVertex =
            {
                [<Position>] pos : V4f
                [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc : V2f
            }

        let pointTransform (v: PointVertex) =
            vertex {
                return V4f(v.pos.XY * 2.0f - 1.0f, 0.0f, 1.0f)
            }

let camera =
    let view = CameraView.look V3d.Zero V3d.YAxis V3d.ZAxis
    let frustum = Frustum.perspective 90.0 0.1 100.0 1.0
    AVal.constant <| Camera.create view frustum

let addTolerance (delta: float) (model: Model) =
    { model with ErrorTolerance = clamp Splines.MinErrorTolerance 0.2 (model.ErrorTolerance + delta) }

let computePathAnimation (model: Model) =
    let startFrom =
        match model |> Animator.tryGetUntyped "Point" with
        | Some c -> c.Position
        | _ -> LocalTime.zero
    
    if model.Points.Length < 2 then
        { model with Position = V2d.NaN }
        |> Animator.remove "Point"

    else
        let animation =
            Animation.Primitives.smoothPath Vec.distance model.ErrorTolerance model.Points
            |> Animation.link Model.Position_
            |> Animation.loop LoopMode.Mirror
            |> Animation.seconds 2.0

        model
        |> Animator.createAndStartFromLocal "Point" animation startFrom

let update (model: Model) (msg: Message) =
    match msg with
    | Add p ->
        { model with Points = Array.append model.Points [| V2d(p.X, 1.0 - p.Y) |] }
        |> computePathAnimation

    | OnKeyDown Keys.Delete ->
        { model with Points = [||] }
        |> computePathAnimation

    | OnKeyDown Keys.Back ->
        let n = max 0 (model.Points.Length - 1)
        { model with Points = model.Points |> Array.take n}
        |> computePathAnimation

    | OnKeyDown Keys.OemPlus ->
        model
        |> addTolerance 0.001
        |> computePathAnimation

    | OnKeyDown Keys.OemMinus ->
        model
        |> addTolerance -0.001
        |> computePathAnimation

    | OnWheel d->
        model
        |> addTolerance (0.0001 * signum d.Y)
        |> computePathAnimation

    | Animation msg ->
        model |> Animator.update msg

    | _ ->
        model

let view (model: AdaptiveModel) =
    let splines =
        adaptive {
            let! pts = model.Points
            let! tol = model.ErrorTolerance
            return Splines.catmullRom Vec.distance tol pts
        }

    let samples =
        splines |> AVal.map (
            Array.collect (fun s -> s.Samples |> Array.map (fun t -> s.Evaluate t))
        )

    let p0 = RenderPass.main
    let p1 = p0 |> RenderPass.after "p1" RenderPassOrder.Arbitrary
    let p2 = p1 |> RenderPass.after "p2" RenderPassOrder.Arbitrary
    let p3 = p2 |> RenderPass.after "p3" RenderPassOrder.Arbitrary

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
            splines |> AVal.map (fun s ->
                s |> Array.collect (fun s ->
                    let n = int <| s.Length * 512.0
                    Array.init n (fun i ->
                        s.Evaluate <| float i / float (n - 1)
                    )
                )
            )
            |> renderPoints p0 C4f.DarkRed 4.0

        let controlPoints =
            model.Points |> renderPoints p1 C4f.AliceBlue 12.0

        let samplePoints =
            samples |> renderPoints p2 C4f.VRVisGreen 8.0

        let positionPoint =
            model.Position
            |> AVal.map Array.singleton
            |> renderPoints p3 C4f.White 4.0
            |> Sg.onOff (model.Position |> AVal.map isFinite)

        Sg.ofList [ controlPoints; samplePoints; splinePoints; positionPoint ]

    body [
        style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden"
    ] [
        renderControl camera [
             style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden"
             onMouseClickRel (fun _ p -> Add p)
             onKeyDown OnKeyDown
             onWheel OnWheel
             onEvent "onRendered" [] (fun _ -> Animation AnimatorMessage.RealTimeTick)
        ] sg

        div [style "position: absolute; left: 10px; top: 10px; color: white; pointer-events: none; font-family: monospace; font-size: larger;"] [
            model.ErrorTolerance
            |> AVal.map (fun t -> $"Error tolerance: %.5f{t}")
            |> Incremental.text
        ]
    ]

let threads (model : Model)=
    model.Animator |> Animator.threads |> ThreadPool.map Animation

let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
               Points = [||]
               Position = V2d.NaN
               ErrorTolerance = 0.01
               Animator = Animator.initial Model.Animator_
            }
        update = update
        view = view
    }