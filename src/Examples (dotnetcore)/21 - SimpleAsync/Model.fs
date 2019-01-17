namespace RenderControl.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Start
    | Log of string
    | Done of (string*float)

[<DomainType>]
type Model = 
    {
        threads : ThreadPool<Message>
        info : string
        result : float
    }