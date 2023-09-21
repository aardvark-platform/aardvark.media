namespace Input

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
        alts      : IndexList<Alternative>
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
    | SetAlternatives of Alternative list
    | SetEnumValue of EnumValue
    | Reset