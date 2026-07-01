namespace Model

open Aardvark.Base
open Adaptify

type Message = 
    | Generate
    | ChangeCount of int
    | ChangeColor of C4b

[<ModelType>]
type Model = 
    {
        count : int
        data : list<float>
        color : C4b
    }