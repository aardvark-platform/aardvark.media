namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | SetContent of string
    | SetPath of string

[<ModelType>]
type Model = 
    {
        FilePath : Option<string>
        Content : string
    }