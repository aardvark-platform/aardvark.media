namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | StartDrag of string * V2d
    | Move of V2d
    | StopDrag of V2d

[<DomainType>]
type Object =
    {
        position : V2d
    }

[<DomainType>]
type Drag = 
    {
        name : string
        startOffset : V2d
    }

[<DomainType>]
type Model = 
    {
        dragObject : Option<Drag>
        objects : hmap<string, Object>
    }