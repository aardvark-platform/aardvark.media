namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Inc of Index
    | Longer



[<ModelType>]
type Model = 
    {
        value : IndexList<int>
        elems : int
    }