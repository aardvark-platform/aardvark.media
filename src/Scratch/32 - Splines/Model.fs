namespace SplinesTest.Model

open Aardvark.Base
open Aardvark.UI.Animation
open Aardvark.Application
open Adaptify

type Message =
    | Add of V2d
    | OnKeyDown of Keys
    | OnWheel of V2d

[<ModelType>]
type Model =
    {
        Points : V2d[]
        ErrorTolerance : float
    }

    member x.Splines =
        Splines.catmullRom Vec.distance x.ErrorTolerance x.Points

    member x.Samples =
        x.Splines |> Array.collect (fun s -> s.Samples |> Array.map (fun t -> s.Evaluate t))