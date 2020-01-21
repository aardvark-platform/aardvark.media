namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.UI
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
type ColorInput = {
    c : C4b
}

[<ModelType>]
type DropDownModel = {
    values   : HashMap<int,string>
    selected : int    
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

[<ModelType>]
type D3TestInput = 
    {
        t1 : int
        t2 : int
    }

[<ModelType>]
type D3AxisInput = 
    {
        min : float
        max : float
        tickCount : float
    }