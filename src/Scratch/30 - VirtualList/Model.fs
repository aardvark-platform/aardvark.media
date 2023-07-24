namespace VirtualListExample.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

[<Struct; ModelType>]
type VirtualHeight =
    {
        itemHeight   : int
        clientHeight : int
    }

[<ModelType>]
type VirtualList<'T> =
    {
        height       : VirtualHeight
        elements     : 'T[]
        scrollOffset : int
        scrollTarget : int
    }

module VirtualList =

    [<RequireQualifiedAccess>]
    type Message =
        | Resize of height: VirtualHeight
        | Scroll of offset: int

    let inline length (list : VirtualList<'T>) =
        list.elements.Length

    let inline ofArray (elements :'T[]) =
        { height       = Unchecked.defaultof<_>
          elements     = elements
          scrollOffset = 0
          scrollTarget = -1 }

    let inline set (elements : 'T seq) (list : VirtualList<'T>) =
        let elements =
            match elements with
            | :? array<'T> as arr -> arr
            | _ -> Array.ofSeq elements

        { list with elements = elements }

    let inline empty<'T> : VirtualList<'T> =
        ofArray Array.empty

    let inline scrollTo (index : int) (list : VirtualList<'T>) =
        let offset = list.height.itemHeight * index
        { list with scrollTarget = offset }


type Message =
    | SetCount of count: int
    | Generate
    | Scroll
    | VirtualListAction of VirtualList.Message

[<ModelType>]
type Model =
    {
        count : int
        elements : VirtualList<string>
    }