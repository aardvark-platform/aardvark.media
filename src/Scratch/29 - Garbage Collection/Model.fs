namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Update
    | Nop

[<ModelType>]
type Model = 
    {
        items : IndexList<string>
    }