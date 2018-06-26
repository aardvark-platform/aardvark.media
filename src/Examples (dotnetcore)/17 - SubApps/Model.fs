namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Increment
    | ResetAll
    | Ping
[<DomainType>]
type Model = 
    {
        dummy : int
    }