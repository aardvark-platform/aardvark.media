namespace Aardvark.UI.Primitives

open Aardvark.UI
open Aardvark.UI.Generic
open FSharp.Data.Adaptive

module Accordion =

    [<RequireQualifiedAccess>]
    type AccordionInput<'msg> =
        | Multi of active: aset<int> * callback: (bool -> int -> 'msg)
        | Single of active: aval<int> * callback: (bool -> int -> 'msg)
        | Empty of exclusive: bool

        member inline x.Callback =
            match x with
            | Multi (_, cb) | Single (_, cb) -> Some cb
            | _ -> None

        member inline x.IsExclusive =
            match x with
            | Single _ | Empty true -> true
            | _ -> false

    module AccordionInternals =

        [<AbstractClass; Sealed>]
        type HeaderConverter() =
            static member inline ToHeader (header: DomNode<'T>)  = header
            static member inline ToHeader (header: aval<string>) = Incremental.text header
            static member inline ToHeader (header: string)       = text header

        let inline private toHeaderAux (_: ^Converter) (header: ^Header) =
            ((^Converter or ^Header) : (static member ToHeader : ^Header -> DomNode<'T>) (header))

        let inline toHeader (header: ^Header) : DomNode<'T> =
            toHeaderAux Unchecked.defaultof<HeaderConverter> header

        let inline toSections (sections: seq< ^Header * DomNode<'msg>>) =
            sections |> Seq.map (fun (h, c) -> toHeader h, c)

        let accordionImpl (input: AccordionInput<'msg>) (attributes: AttributeMap<'msg>) (sections: seq<DomNode<'msg> * DomNode<'msg>>) =
            let dependencies =
                Html.semui @ [ { name = "accordion"; url = "resources/accordion.js"; kind = Script }]

            let attributes =
                let basic =
                    AttributeMap.ofList [
                        clazz "ui accordion"

                        match input.Callback with
                        | Some cb ->
                            onEvent "onopen" [] (List.head >> int >> cb true)
                            onEvent "onclose" [] (List.head >> int >> cb false)

                        | _ -> ()
                    ]

                AttributeMap.union attributes basic

            let channel =
                match input with
                | AccordionInput.Multi (set, _) ->
                    Some (ASet.channel set)

                | AccordionInput.Single (index, _) ->
                    index |> AVal.map (fun i ->
                        if i < 0 then SetOperation.Rem -1   // Handle in JS, we don't know the actual index here
                        else SetOperation.Add i
                    )
                    |> AVal.channel
                    |> Some

                | _ -> None

            let boot =
                let exclusive = if input.IsExclusive then "true" else "false"
                let channel = if channel.IsSome then "channelActive" else "null"

                String.concat "" [
                    "const $self = $('#__ID__');"
                    "aardvark.accordion($self, " + exclusive + ", " + channel + ");"
                ]

            let channels =
                match channel with
                | Some ch -> [ "channelActive", ch ]
                | _ -> []

            let isActive =
                let set =
                    match input with
                    | AccordionInput.Multi (set, _) -> set |> ASet.toAVal |> AVal.force
                    | AccordionInput.Single (index, _) -> index |> AVal.force |> HashSet.single
                    | _ -> HashSet.empty

                fun i -> set |> HashSet.contains i

            require dependencies (
                onBoot' channels boot (
                    Incremental.div attributes <| AList.ofList [
                        let sections = Array.ofSeq sections

                        for index = 0 to sections.Length - 1 do
                            let title, node = sections.[index]
                            let active = isActive index

                            div [clazz "title"; if active then clazz "active"] [
                                i [clazz "dropdown icon"] []
                                title
                            ]
                            div [clazz "content"; if active then clazz "active"] [
                                node
                            ]
                    ]
                )
            )

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// The active set holds the indices of the open sections.
    /// The toggle (index, isOpen) message is fired when a section is opened or closed.
    /// The section headers can be provided as DomNode, aval<string>, or string.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    let inline accordion (toggle: int * bool -> 'msg) (active: aset<int>) attributes (sections: seq< ^Header * DomNode<'msg>>) =
        let cb s i = toggle (i, s)
        AccordionInternals.toSections sections |> AccordionInternals.accordionImpl (AccordionInput.Multi (active, cb)) (att attributes)

    /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
    /// The active value holds the index of the open section, or -1 if there is no open section.
    /// The setActive (index | -1) message is fired when a section is opened or closed.
    /// The section headers can be provided as DomNode, aval<string>, or string.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    let inline accordionExclusive (setActive: int -> 'msg) (active: aval<int>) attributes (sections: seq< ^Header * DomNode<'msg>>) =
        let cb s i = (if s then i else -1) |> setActive
        AccordionInternals.toSections sections |> AccordionInternals.accordionImpl (AccordionInput.Single (active, cb)) (att attributes)

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// If exclusive is true, only one section can be open at a time.
    /// The section headers can be provided as DomNode, aval<string>, or string.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    let inline accordionSimple (exclusive: bool) attributes (sections: seq< ^Header * DomNode<'msg>>) =
        AccordionInternals.toSections sections |> AccordionInternals.accordionImpl (AccordionInput.Empty exclusive) (att attributes)