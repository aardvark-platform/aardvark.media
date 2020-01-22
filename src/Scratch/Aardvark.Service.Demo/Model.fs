namespace Demo.TestApp

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Mutable
open Aardvark.UI
open FShade.Primitives
open Aardvark.Application

type ClientLocalAttribute() = inherit System.Attribute()

[<ModelType>]
type Urdar = { urdar : int }


[<ModelType>]
type TreeNode<'a> =
    {
        content : 'a
        children : IndexList<TreeNode<'a>>
    }

[<ModelType>]
type Tree<'a> =
    {
        nodes : IndexList<TreeNode<'a>>
    }


[<ModelType>]
type Model =
    {
        boxHovered      : bool
        dragging        : bool
        lastName        : Option<string>
        elements        : IndexList<string>
        hasD3Hate       : bool
        boxScale        : float
        objects         : HashMap<string,Urdar>
        lastTime        : MicroTime
        tree            : Tree<int>
    }


