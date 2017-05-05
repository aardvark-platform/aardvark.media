namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Mutable
open Aardvark.UI
open FShade.Primitives
open Aardvark.Application

type ClientLocalAttribute() = inherit System.Attribute()

[<DomainType>]
type Urdar = { urdar : int }

[<DomainType>]
type Model =
    {
        boxHovered      : bool
        dragging        : bool
        lastName        : Option<string>
        elements        : plist<string>
        hasD3Hate       : bool
        boxScale        : float
        objects         : hmap<string,Urdar>
        lastTime        : MicroTime
    }



