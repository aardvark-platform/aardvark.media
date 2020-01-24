namespace Inc.Model

open System
open System.Diagnostics
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Exectuable = DotnetAssembly of string | Native of string

type Endpoint = 
    {
        assembly         : Exectuable
        workingDirectory : string
        prettyName       : string
        url              : int -> string
    }

type Id = string

type InstanceMessage = 
    | Stdout of Id * string 
    | Stderr of Id * string
    | Exit of Id

type Message = 
    | Start of Endpoint
    | InstanceStatus of InstanceMessage
    | Kill of Id

[<ModelType>]
type Instance = 
    {
        [<NonAdaptive>]
        p : Process
        [<NonAdaptive>]
        id : string
        [<NonAdaptive>]
        port : int
        [<NonAdaptive>]
        endpoint : Endpoint
    }

module Instance = 
    let getUrl (i : Instance) = 
        i.endpoint.url i.port

[<ModelType>]
type Model = 
    {
        executables : IndexList<Endpoint>
        running : HashMap<string,Instance>
    }