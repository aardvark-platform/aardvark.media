namespace DiscoverOpcs.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | SetPaths of list<string>
    | Discover

[<DomainType>]
type Model = 
    {
        selectedPaths : plist<string>
        opcPaths     : plist<string>
    }