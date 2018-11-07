namespace Chat

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

open System


type Client = 
    {
        id : string
        name : string
    }

[<DomainType>]
type Model = 
    {
        lines : hmap<DateTime, string>
        clients : hmap<string, Client>
        currentMsg : hmap<string, string>
    }

module Model =
    let initial =
        {
            lines = HMap.empty
            clients = HMap.empty
            currentMsg = HMap.empty
        }

