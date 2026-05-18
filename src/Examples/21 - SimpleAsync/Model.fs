namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Start
    | Log of string
    | Done of (string*float)

[<ModelType>]
type Model = 
    {
        threads : ThreadPool<Message>
        info : string
        result : float
    }