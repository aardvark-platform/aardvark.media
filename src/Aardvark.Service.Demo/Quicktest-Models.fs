namespace QuickTest

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Person = {
    firstName : string
    secondName : string
}

[<DomainType>]
type QuickTestModel = {
    values : plist<Person>
    selected : string
    newValue : Person
}