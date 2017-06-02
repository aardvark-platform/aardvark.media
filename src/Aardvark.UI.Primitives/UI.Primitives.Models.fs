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
