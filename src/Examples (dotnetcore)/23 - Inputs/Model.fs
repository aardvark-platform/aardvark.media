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
type MegaNumber = 
    {   
        value : float // now we have adaptive changes and do not lose the focus in numeric input!
    }

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

        testHashMap : HashMap<string, MegaNumber>
        brokenHashMap : HashMap<string, float>

        // sort and changeable list based on helper-structure
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

    | SortInputName of string
    | SortAddSetItem of string * string * Alternative
    | SortUpdateSorting of string * string
    | SortUpdateValue of string * Alternative
    | SortRemoveItem of string

    | TestHashMapChange of string * float
    | BrokenHashMapChange of string * float