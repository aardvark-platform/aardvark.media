﻿namespace Aardvark.UI.Primitives
 
open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

[<ModelType>]
type NumericInput = {
    value : float
    min   : float
    max   : float
    step  : float
    format: string
}

[<ModelType>]
type V3dInput = {
    x : NumericInput
    y : NumericInput
    z : NumericInput
    value : V3d
}

[<ModelType>]
type LeafValue = 
    | Number of number : int 
    | Text of text : string

[<ModelType>]
type Properties = { isExpanded : bool; isSelected : bool; isActive : bool }

[<ModelType>]
type Tree =
    | Node of value : LeafValue * properties : Properties * children : IndexList<Tree>
    | Leaf of value : LeafValue

[<AutoOpen; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =
    let node v p c = Node(v, p, c)

[<ModelType>]
type TreeModel = { data: Tree }