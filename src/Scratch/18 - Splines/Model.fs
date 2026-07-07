namespace SplinesTest.Model

open Aardvark.Base
open Aardvark.UI.Animation
open Aardvark.Application
open Adaptify

[<ModelType>]
type Model =
    {
        Points : V2d[]
        Position : V2d
        ErrorTolerance : float

        [<NonAdaptive>]
        Animator : Animator<Model>
    }

type Message =
    | Add of V2d
    | OnKeyDown of Keys
    | OnWheel of V2d
    | Animation of AnimatorMessage<Model>