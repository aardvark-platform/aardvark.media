namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type RelativeClick =
    {
        relativeToContainer : V2d
        relativeToElement : V2d
    }


type Message = 
    | StartDrag of string * RelativeClick
    | Move of V2d
    | StopDrag of V2d

[<ModelType>]
type Object =
    {
        position : V2d
    }

[<ModelType>]
type Drag = 
    {
        name : string
        startOffset : V2d
    }

[<ModelType>]
type Model = 
    {
        dragObject : Option<Drag>
        objects : HashMap<string, Object>
    }