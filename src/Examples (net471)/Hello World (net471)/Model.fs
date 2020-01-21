namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Inc

[<ModelType>]
type Model = 
    {
        value : int
    }