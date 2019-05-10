namespace Inc.Model

open System
open System.Diagnostics
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

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

[<DomainType>]
type Instance = 
    {
        [<NonIncremental>]
        p : Process
        [<NonIncremental>]
        id : string
        [<NonIncremental>]
        port : int
        [<NonIncremental>]
        endpoint : Endpoint
    }

module Instance = 
    let getUrl (i : Instance) = 
        i.endpoint.url i.port

[<DomainType>]
type Model = 
    {
        executables : plist<Endpoint>
        running : hmap<string,Instance>
    }