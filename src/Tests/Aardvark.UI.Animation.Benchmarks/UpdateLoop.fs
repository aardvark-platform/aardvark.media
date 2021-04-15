namespace Aardvark.UI.Animation.Benchmarks

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI.Anewmation
open BenchmarkDotNet.Attributes;

open Aether

type Model =
    {
        SomeInt : int
        SomeFloat : float
        SomeVector : V3d
        SomeCamera : CameraView
        Animator : Animator<Model>
    }

module Model =

    let SomeInt_ : Lens<Model, int> =
        (fun model -> model.SomeInt),
        (fun value model -> { model with SomeInt = value })

    let SomeFloat_ : Lens<Model, float> =
        (fun model -> model.SomeFloat),
        (fun value model -> { model with SomeFloat = value })

    let SomeVector_ : Lens<Model, V3d> =
        (fun model -> model.SomeVector),
        (fun value model -> { model with SomeVector = value })

    let SomeCamera_ : Lens<Model, CameraView> =
        (fun model -> model.SomeCamera),
        (fun value model -> { model with SomeCamera = value })

    let Animator_ : Lens<Model, Animator<Model>> =
        (fun model -> model.Animator),
        (fun value model -> { model with Animator = value })

    let initial =
        {
            SomeInt = 0
            SomeFloat = 0.0
            SomeVector = V3d.Zero
            SomeCamera = CameraView.look V3d.Zero V3d.XAxis V3d.ZAxis
            Animator = Animator.initial Animator_
        }

module Animations =

    let private simpleIntLerp (rnd : RandomSystem) =
        Animation.Primitives.lerp (rnd.UniformInt(32)) (rnd.UniformInt(256))
        |> Animation.link Model.SomeInt_
        |> Animation.seconds 0.5
        |> Animation.ease (Easing.InOut <| EasingFunction.Bounce 1.0)
        |> Animation.loop LoopMode.Mirror

    let private simpleFloatLerp (rnd : RandomSystem) =
        Animation.Primitives.lerp (rnd.UniformDouble() * 16.0) (rnd.UniformDouble() * 256.0)
        |> Animation.link Model.SomeFloat_
        |> Animation.seconds 1.3333
        |> Animation.ease (Easing.InOut EasingFunction.Sine)
        |> Animation.loop LoopMode.Mirror

    let private linearPath (rnd : RandomSystem) =
        let count = 6 + rnd.UniformInt(48)

        Array.init count (fun _ -> rnd.UniformV3d() * rnd.UniformDouble() * 10.0)
        |> Animation.Primitives.linearPath Vec.distance
        |> Animation.link Model.SomeVector_
        |> Animation.seconds 3.0
        |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
        |> Animation.loop LoopMode.Mirror

    let add (rnd : RandomSystem) (count : int) (model : Model) =
        (model, [| 1 .. count |]) ||> Array.fold (fun model i ->
            model
            |> Animator.createAndStart (Sym.ofString (sprintf "int%d" i)) (simpleIntLerp rnd)
            |> Animator.createAndStart (Sym.ofString (sprintf "float%d" i)) (simpleFloatLerp rnd)
            |> Animator.createAndStart (Sym.ofString (sprintf "path%d" i)) (linearPath rnd)
        )

[<MemoryDiagnoser>]
type UpdateLoop() =
    [<DefaultValue; Params(10, 50, 100)>]
    val mutable Count : int

    let iterations = 1000
    let mutable model = Unchecked.defaultof<_>

    [<GlobalSetup>]
    member x.Init() =
        let rnd = RandomSystem(0)
        model <- Model.initial |> Animations.add rnd x.Count

    [<Benchmark>]
    member x.UpdateLoop() =
        for _ = 0 to iterations - 1 do
            model <- model |> Animator.update AnimatorMessage.Tick

        model.SomeInt
