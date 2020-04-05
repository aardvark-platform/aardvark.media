namespace Chat

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

open System
open Adaptify

type Client = 
    {
        id : string
        name : string
    }

[<ModelType>]
type Model = 
    {
        lines : HashMap<DateTime, string>
        clients : HashMap<string, Client>
        currentMsg : HashMap<string, string>
    }

module Model =
    let initial =
        {
            lines = HashMap.empty
            clients = HashMap.empty
            currentMsg = HashMap.empty
        }

