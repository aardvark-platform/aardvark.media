namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | OpenFiles of list<string>

[<DomainType>]
type Model = 
    {
        currentFiles : plist<string>
    }