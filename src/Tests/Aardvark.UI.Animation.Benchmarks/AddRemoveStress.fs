namespace Aardvark.UI.Animation.Benchmarks

open Aardvark.Base
open Aardvark.UI.Animation
open BenchmarkDotNet.Attributes;

[<MemoryDiagnoser>]
type AddRemoveStress() =

    [<DefaultValue; Params(10, 50, 100)>]
    val mutable Count : int

    let iterations = 1000
    let mutable model = Unchecked.defaultof<_>

    [<GlobalSetup>]
    member x.Init() =
        let rnd = RandomSystem(0)
        model <- Model.initial |> Animations.add rnd x.Count

    [<Benchmark>]
    member x.Run() =
        let rnd = RandomSystem(0)

        for _ = 0 to iterations - 1 do

            for _ = 0 to rnd.UniformInt(x.Count / 2) do
                let name = Animations.getRandomName rnd x.Count

                match model |> Animator.tryGetUntyped name with
                | Some inst ->
                    model <- model |> Animator.remove name
                    model <- model |> Animator.createAndStart name inst.Definition

                | _ -> ()

            model <- model |> Animator.update AnimatorMessage.RealTimeTick

        model.SomeInt
