namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Increment
    | ResetAll
    | Ping
[<ModelType>]
type Model = 
    {
        dummy : int
    }