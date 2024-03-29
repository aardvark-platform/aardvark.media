﻿namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Inc of Index
    | SetMin of int
    | SetCount of int

[<ModelType>]
type ListSlice = 
    {
        min : int
        count : int
    }

[<ModelType>]
type Model = 
    {
        value : IndexList<int>
        elems : ListSlice
    }