namespace Input

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Model =
    {
        active  : bool
        value   : float
        name    : string
    }

type Message = 
    | ToggleActive
    | SetValue of float
    | SetName of string