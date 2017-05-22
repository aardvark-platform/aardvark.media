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

//[<DomainType>]
//type LeafValue = 
//    | Number of int 
//    | Text of string

//[<DomainType>]
//type Properties = { isExpanded : bool; isSelected : bool; isActive : bool }

//[<DomainType>]
//type Tree =
//    | Node of value : LeafValue * properties : Properties * children : plist<Tree>
//    | Leaf of value : LeafValue

//[<AutoOpen>]
//module Tree =
//    let node v p c = Node(v, p, c)


//[<DomainType>]
//type TreeModel = { data: Tree }



module Lol =
    let mutable currentId = 0
open Lol
type NodeData =
    {
        key : string
        title : string
        folder : bool
        customIcon : Option<string>
        tooltip : string
    }
        
module NodeData=

    let node title folder customIcon tooltip =
        currentId <- System.Threading.Interlocked.Increment &currentId
        {
            key = string currentId
            title = title
            folder = folder
            customIcon = customIcon
            tooltip = tooltip
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

type TreeViewTree =
| Empty
| Leaf of NodeData
| Node of NodeData * list<TreeViewTree>

[<DomainType>]
type TreeViewModel =
    {
        tree : TreeViewTree
    }