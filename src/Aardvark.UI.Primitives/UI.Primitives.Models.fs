namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.UI

[<DomainType>]
type NumericBox = {
    value : float
    min   : float
    max   : float
    step  : float
    format: string
}

