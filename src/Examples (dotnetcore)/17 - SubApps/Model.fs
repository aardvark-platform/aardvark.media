namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Increment

[<DomainType>]
type Model = 
    {
        dummy : int
    }