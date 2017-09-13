namespace QuickTest

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

[<DomainType>]
type DropDownModel = {
    values   : plist<string>
    selected : string
    newValue : string
}