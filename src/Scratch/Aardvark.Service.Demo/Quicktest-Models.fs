namespace QuickTest

open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Person = {
    firstName : string
    secondName : string
}

[<ModelType>]
type QuickTestModel = {
    values : IndexList<Person>
    selected : Person
    newValue : option<Person>
}