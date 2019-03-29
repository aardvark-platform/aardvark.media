namespace Inc.Model

open System
open System.Diagnostics
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Endpoint = 
    {
        assembly         : string
        workingDirectory : string
        prettyName       : string
        url              : int -> string
    }

type Id = int

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
        id : int
        [<NonIncremental>]
        port : int
        [<NonIncremental>]
        endpoint : Endpoint
    }

[<DomainType>]
type Model = 
    {
        nextClientId : int
        executables : plist<Endpoint>
        running : hmap<int,Instance>
    }