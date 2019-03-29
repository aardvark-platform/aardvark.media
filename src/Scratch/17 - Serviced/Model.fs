﻿namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Inc

[<DomainType>]
type Model = 
    {
        value : int
    }