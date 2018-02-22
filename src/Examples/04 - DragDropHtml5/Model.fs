namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | DropTop
    | DropBottom

type Position = Top = 0 | Bottom = 1

[<DomainType>]
type Model = 
    {
        location : Position
    }