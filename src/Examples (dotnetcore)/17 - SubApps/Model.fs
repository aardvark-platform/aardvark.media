namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Increment
    | ResetAll
    | Ping
[<ModelType>]
type Model = 
    {
        dummy : int
    }