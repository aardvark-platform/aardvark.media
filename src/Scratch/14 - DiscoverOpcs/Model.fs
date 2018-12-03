namespace DiscoverOpcs.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open DiscoverOpcs

type Message = 
    | SetPaths of list<string>
    | Discover

[<DomainType>]
type Model = 
    {
        selectedPaths : plist<OpcFolder>
        opcPaths      : plist<string>
    }