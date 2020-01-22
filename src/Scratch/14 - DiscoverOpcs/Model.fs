namespace DiscoverOpcs.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open DiscoverOpcs

type Message = 
    | SetPaths of list<string>
    | Discover

[<ModelType>]
type Model = 
    {
        selectedPaths : IndexList<string>
        opcPaths      : HashMap<string, list<string>>
        surfaceFolder : list<string>
    }