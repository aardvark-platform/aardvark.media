namespace Aardvark.UI.Primitives

open System
open Suave

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI

[<AutoOpen>]
module TreeViewFancyModel =

    type NodeData =
        {
            key : string
            title : string
            folder : bool
            customIcon : Option<string>
            tooltip : string
        }
    
    type TreePointer = 
        | Root
        | Selected
        | Node of key : string

    type InsertMode =
        | Before
        | After
        | FirstChild
        | LastChild

    type InsertPostion = { pointer : TreePointer; mode : InsertMode }

    type TreeViewMessage = 
        | Add of location : InsertPostion * node : NodeData 
        | Remove of location : TreePointer
        | Move of source : TreePointer * target : InsertPostion
        | Select of location : TreePointer
        | Deselect of location : TreePointer