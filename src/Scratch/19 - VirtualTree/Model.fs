namespace VirtualTreeExample.Model

open Aardvark.Base
open Adaptify

open TreeView.Model

[<ModelType>]
type Item =
    { icon  : string
      label : string
      color : C3b }

type Message =
    | SetCount of count: int
    | Generate
    | Scroll
    | TreeViewAction of TreeView.Message<int>

[<ModelType>]
type Model =
    {
        count : int
        treeView : TreeView<int, Item>
    }