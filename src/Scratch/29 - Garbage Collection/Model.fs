namespace GarbageApp

open FSharp.Data.Adaptive
open Adaptify

type Message = 
    | Update of byte[]

[<ModelType>]
type Model = 
    {
        items : IndexList<string>
    }