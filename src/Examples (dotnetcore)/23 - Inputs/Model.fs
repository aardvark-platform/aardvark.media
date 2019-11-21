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
        active    : bool
        value     : float
        intValue  : int
        decValue  : decimal
        uintValue : uint32
        name      : string
        alt       : Option<Alternative>
        options   : hmap<Alternative, string>
    }

type Message = 
    | ToggleActive
    | SetValue of float
    | SetInt of int
    | SetDecimal of decimal
    | SetUInt of uint32
    | SetName of string
    | SetAlternative of Option<Alternative>