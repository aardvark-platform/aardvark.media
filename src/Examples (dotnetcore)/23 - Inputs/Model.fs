namespace Input

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

type Alternative =
    | A
    | B
    | C
    | D
    | Custom of string

[<ModelType>]
type Model =
    {
        active  : bool
        value   : float
        name    : string
        alt     : Option<Alternative>
        options : HashMap<Alternative, string>
    }

type Message = 
    | ToggleActive
    | SetValue of float
    | SetName of string
    | SetAlternative of Option<Alternative>