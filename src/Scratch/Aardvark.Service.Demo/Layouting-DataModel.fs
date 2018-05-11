namespace LayoutingModel

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Tab = { name : string; url : string }

[<DomainType>]
type Tree = 
    | Vertical of Tree * Tree
    | Horizontal of Tree * Tree
    | Leaf of Tab

[<DomainType>]
type Model = { 
    tabs : plist<Tab>
}

type Action = Action