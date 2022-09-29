namespace Tmp.Inc

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Inc

[<ModelType>]
type Model = 
    {
        value : int
        id    : string
    } with
    static member init id =
        { 
            value = 0
            id    = id
        }