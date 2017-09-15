namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.UI

[<DomainType>]
type NumericInput = {
    value : float
    min   : float
    max   : float
    step  : float
    format: string
}

[<DomainType>]
type V3dInput = {
    x : NumericInput
    y : NumericInput
    z : NumericInput
    value : V3d
}

[<DomainType>]
type ColorInput = {
    c : C4b
}

[<DomainType>]
type DropDownModel = {
    values   : hmap<int,string>
    selected : int    
}

[<DomainType>]
type LeafValue = 
    | Number of int 
    | Text of string

[<DomainType>]
type Properties = { isExpanded : bool; isSelected : bool; isActive : bool }

[<DomainType>]
type Tree =
    | Node of value : LeafValue * properties : Properties * children : plist<Tree>
    | Leaf of value : LeafValue

[<AutoOpen; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =
    let node v p c = Node(v, p, c)


[<DomainType>]
type TreeModel = { data: Tree }
