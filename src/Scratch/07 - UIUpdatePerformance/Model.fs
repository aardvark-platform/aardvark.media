namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | Go
    | Done

[<DomainType>]
type Model = 
    {
        value : int
        threads : ThreadPool<Message>
        updateStart : float
        took : float
        things : plist<string>
    }