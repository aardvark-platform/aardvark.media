namespace Model

open Adaptify
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | ChooseFiles of string list
    | ChooseFolder of string

[<ModelType>]
type Model = 
    {
        value : int
    }