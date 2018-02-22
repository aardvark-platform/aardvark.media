namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Message = 
    | Generate
    | ChangeCount of Numeric.Action


[<DomainType>]
type Model = 
    {
        count : NumericInput
        data : list<float>
    }