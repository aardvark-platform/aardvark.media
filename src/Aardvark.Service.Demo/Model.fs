namespace Demo.TestApp

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Model =
    {
        lastName    : Option<string>
        elements    : plist<string>
        hasD3Hate   : bool
        boxScale    : float
        boxHovered  : bool
        dragging    : bool
    }