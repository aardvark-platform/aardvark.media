namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | OpenFiles of list<string>

[<ModelType>]
type Model = 
    {
        currentFiles : IndexList<string>
    }