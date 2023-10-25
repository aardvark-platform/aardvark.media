namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Generate
    | ChangeCount of Numeric.Action
    | ChangeColor of C4b


[<ModelType>]
type Model = 
    {
        count : NumericInput
        data : list<float>

        color : C4b
    }