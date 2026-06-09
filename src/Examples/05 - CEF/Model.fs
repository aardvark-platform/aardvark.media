namespace Model

open Adaptify

type Message = 
    | Inc

[<ModelType>]
type Model = 
    {
        value : int
    }