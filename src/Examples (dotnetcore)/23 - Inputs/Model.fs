namespace Input

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

type Alternative =
    | A
    | B
    | C
    | D
    | Custom of string

type EnumValue =
    | Value1 = 1
    | Value2 = 2
    | Value3 = 3

[<ModelType>]
type Model =
    {
        active    : bool
        value     : float
        intValue  : int
        decValue  : decimal
        uintValue : uint32
        name      : string
        alt       : Option<Alternative>
        options : HashMap<Alternative, string>
        enumValue : EnumValue
    }

type Message = 
    | ToggleActive
    | SetValue of float
    | SetInt of int
    | SetDecimal of decimal
    | SetUInt of uint32
    | SetName of string
    | SetAlternative of Option<Alternative>
    | SetEnumValue of EnumValue
    | Reset