namespace Input

open Aardvark.Base
open Aardvark.Base.Incremental


type Alternative =
    | A
    | B
    | C
    | D
    | Custom of string

[<DomainType>]
type Model =
    {
        active  : bool
        value   : float
        name    : string
        alt     : Option<Alternative>
        options : hmap<Alternative, string>
    }

type Message = 
    | ToggleActive
    | SetValue of float
    | SetName of string
    | SetAlternative of Option<Alternative>