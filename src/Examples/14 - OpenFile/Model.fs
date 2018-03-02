namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | OpenFile of string

[<DomainType>]
type Model = 
    {
        currentFile : string
    }