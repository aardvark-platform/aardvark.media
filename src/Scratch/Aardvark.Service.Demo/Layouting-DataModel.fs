namespace LayoutingModel

open Aardvark.Base
open FSharp.Data.Adaptive

[<ModelType>]
type Tab = { name : string; url : string }

[<ModelType>]
type Tree = 
    | Vertical of Tree * Tree
    | Horizontal of Tree * Tree
    | Leaf of Tab

[<ModelType>]
type Model = { 
    tabs : IndexList<Tab>
}

type Action = Action