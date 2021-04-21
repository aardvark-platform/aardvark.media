namespace Aardvark.UI.Animation.Benchmarks

open Aardvark.Base
open Aardvark.UI.Anewmation
open BenchmarkDotNet.Attributes;

[<MemoryDiagnoser>]
type SimpleLoop() =

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
        for _ = 0 to iterations - 1 do
            model <- model |> Animator.update AnimatorMessage.RealTimeTick

        model.SomeInt
