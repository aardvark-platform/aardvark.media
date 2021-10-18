namespace Input

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify
open SortedHashMap

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
        options   : HashMap<Alternative, string>
        enumValue : EnumValue

        // changeable list
        itemMap : HashMap<string, float * Alternative>
        itemList : IndexList<string>
        inputText : string
        inputValue : float

        // sort and changeable list
        [<NonAdaptive>]
        itemSortedHelper: SortedHashMap<string, string * Alternative>
        itemSortedMap : HashMap<string, string * Alternative>
        itemSortedList : IndexList<string>
        inputName : string
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

    | SetInputValue of float
    | SetItem of string * float * Alternative
    | UpdateItemV1 of string * float
    | UpdateItemV2 of string * Alternative
    | RemoveItem of string

    | SortInputName of string
    | SortSetItem of string * string * Alternative
    | SortUpdateItemV1 of string * string
    | SortUpdateItemV2 of string * Alternative
    | SortRemoveItem of string