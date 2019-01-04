namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open System

type Message = 
    | Inc
    | Go
    | Done
    | Super
    | Tick
    | GotImage of DateTime

[<DomainType>]
type Model = 
    {
        value : int
        super : int
        threads : ThreadPool<Message>
        updateStart : float
        took : float
        things : plist<string>
        angle : float
        lastImage : DateTime
    }