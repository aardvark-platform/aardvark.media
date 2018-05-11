namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type RelativeClick =
    {
        relativeToContainer : V2d
        relativeToElement : V2d
    }


type Message = 
    | StartDrag of string * RelativeClick
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