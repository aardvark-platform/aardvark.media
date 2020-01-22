namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | DropTop
    | DropBottom

type Position = Top = 0 | Bottom = 1

[<ModelType>]
type Model = 
    {
        location : Position
    }