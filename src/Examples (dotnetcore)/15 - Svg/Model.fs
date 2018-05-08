namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | StartDrag of V2d
    | Drag of V2d

[<DomainType>]
type Model = 
    {
        startPos : Option<V2d>
        pos : V2d
    }