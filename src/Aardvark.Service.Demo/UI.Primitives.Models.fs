namespace UiPrimitives

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.UI


type Numeric = {    
    value : float
    min   : float
    max   : float
    step  : float
    format: string
}

[<DomainType>]
type NumericBox = {
    value : Numeric
}

