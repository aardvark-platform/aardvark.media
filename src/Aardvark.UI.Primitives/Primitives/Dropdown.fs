namespace Aardvark.UI.Primitives

open System
open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open Aardvark.UI.Generic

module Dropdown =

    [<RequireQualifiedAccess>]
    type DropdownValues<'T, 'msg> =
        | List of alist<'T * DomNode<'msg>>
        | Map of amap<'T, DomNode<'msg>>

    module DropdownInternals =

        [<AbstractClass; Sealed>]
        type DropdownValueConverter() =
            static member inline ToDropdownValues (values: DropdownValues<'T, 'msg>)  = values
            static member inline ToDropdownValues (values: alist<'T * DomNode<'msg>>) = DropdownValues.List values
            static member inline ToDropdownValues (values: amap<'T, DomNode<'msg>>)   = DropdownValues.Map values
            static member inline ToDropdownValues (values: seq<'T * DomNode<'msg>>)   = DropdownValues.List <| AList.ofSeq values
            static member inline ToDropdownValues (values: array<'T * DomNode<'msg>>) = DropdownValues.List <| AList.ofArray values
            static member inline ToDropdownValues (values: list<'T * DomNode<'msg>>)  = DropdownValues.List <| AList.ofList values

        let inline private toDropdownValuesAux (_: ^Converter) (values: ^Values) =
            ((^Converter or ^Values) : (static member ToDropdownValues : ^Values -> DropdownValues<'T, 'msg>) (values))

        let inline toDropdownValues (values: ^Values) : DropdownValues<'T, 'msg> =
            toDropdownValuesAux Unchecked.defaultof<DropdownValueConverter> values

        let inline getEnumValues<'T, 'U, 'msg when 'T : enum<'U>> (toNode: Option<'T -> DomNode<'msg>>) : array<'T * DomNode<'msg>> =
            let values = Enum.GetValues typeof<'T> |> unbox<'T[]>
            let nodes =
                match toNode with
                | Some f -> values |> Array.map f
                | _ -> Enum.GetNames typeof<'T> |> Array.map text

            Array.zip values nodes

        let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

        let private dropdownImpl (update: 'T list -> 'msg) (activateOnHover: bool) (placeholder: string option) (multiSelect: bool) (icon: string option)
                                 (selected: alist<'T>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            let dependencies =
                Html.semui @ [ { name = "dropdown"; url = "resources/dropdown.js"; kind = Script }]

            let valuesWithKeys =
                let sortedValues =
                    match values with
                    | DropdownValues.List list -> list
                    | DropdownValues.Map map ->
                        let cmp =
                            if typeof<System.IComparable>.IsAssignableFrom typeof<'T> then Unchecked.compare<'T>
                            else fun _ _ -> -1

                        map |> AMap.toASet |> ASet.sortWith (fun (a, _) (b, _) -> cmp a b)

                sortedValues
                |> AList.map (fun (key, node) ->
                    let hash = pickler.ComputeHash(key).Hash |> Convert.ToBase64String
                    key, hash, node
                )

            let lookup =
                valuesWithKeys
                |> AList.toAVal
                |> AVal.map (fun values ->
                    let forward = values |> IndexList.map (fun (key, hash, _) -> struct (key, hash)) |> HashMap.ofSeqV
                    let backward = values |> IndexList.map (fun (key, hash, _) -> struct (hash, key)) |> HashMap.ofSeqV
                    forward, backward
                )

            let update (args : string list) =
                try
                    let data : string = Pickler.unpickleOfJson args.Head
                    let _fw, bw = AVal.force lookup

                    let values =
                        data
                        |> String.split ","
                        |> Array.toList
                        |> List.choose (fun k -> HashMap.tryFind k bw)

                    Seq.singleton (update values)

                with exn ->
                    Log.warn "[Dropdown] callback failed: %s" exn.Message
                    Seq.empty

            let selection =
                adaptive {
                    let! selected = selected |> AList.toAVal
                    let! fw, _bw = lookup

                    return selected
                        |> IndexList.choose (fun v -> HashMap.tryFind v fw)
                        |> IndexList.toList
                }

            let attributes =
                let disableClazz clazz disabled =
                    if disabled then AttributeMap.removeClass clazz
                    else id

                let toggleClazz clazz enabled =
                    if enabled then AttributeMap.addClass clazz
                    else AttributeMap.removeClass clazz

                let attributes =
                    attributes
                    |> toggleClazz "multiple" multiSelect
                    |> toggleClazz "selection" icon.IsNone
                    |> disableClazz "clearable" placeholder.IsNone

                AttributeMap.ofList [
                    clazz "ui dropdown"
                    onEvent' "data-event" [] update
                ]
                |> AttributeMap.union attributes

            let boot =
                let trigger = if activateOnHover then "'hover'" else "'click'"

                String.concat "" [
                    "const $self = $('#__ID__');"
                    $"aardvark.dropdown($self, {trigger}, channelSelection);"
                ]

            require dependencies (
                onBoot' ["channelSelection", AVal.channel selection] boot (
                    Incremental.div attributes <| AList.ofList [
                        input [ attribute "type" "hidden" ]

                        match icon with
                        | Some icon ->
                            i [ clazz (sprintf "%s icon" icon)] []
                        | _ ->
                            i [ clazz "dropdown icon" ] []
                            div [ clazz "default text"] (placeholder |> Option.defaultValue "")

                        Incremental.div (AttributeMap.ofList [clazz "menu"]) <| alist {
                            for (_, hash, node) in valuesWithKeys do
                                yield div [ clazz "ui item"; attribute "data-value" hash] [node]
                        }
                    ]
                )
            )

        let private dropdownOptionImpl (update: 'T option -> 'msg) (activateOnHover: bool) (icon: string option) (placeholder: string)
                                       (selected: aval<'T option>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            let selected = selected |> AVal.map Option.toList |> AList.ofAVal
            let update = List.tryHead >> update
            dropdownImpl update activateOnHover (Some placeholder) false icon selected attributes values

        let private dropdownSingleImpl (update: 'T -> 'msg) (activateOnHover: bool) (icon: string option)
                                       (selected: aval<'T>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            let selected = selected |> AVal.map List.singleton |> AList.ofAVal
            let update = List.head >> update
            dropdownImpl update activateOnHover None false icon selected attributes values

        /// Dropdown menu for a collection of values. Multiple items can be selected at a time.
        let dropdownMultiSelect (update: 'T list -> 'msg) (activateOnHover: bool) (placeholder: string)
                                (selected: alist<'T>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            dropdownImpl update activateOnHover (Some placeholder) true None selected attributes values

        /// Dropdown menu for a collection of values. At most a single item can be selected at a time.
        let dropdownOption (update: 'T option -> 'msg) (activateOnHover: bool) (icon: string option) (placeholder: string)
                           (selected: aval<'T option>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            dropdownOptionImpl update activateOnHover icon placeholder selected attributes values

        /// Dropdown menu for a collection of values.
        let dropdown (update: 'T -> 'msg) (activateOnHover: bool) (icon: string option)
                     (selected: aval<'T>) (attributes: AttributeMap<'msg>) (values: DropdownValues<'T, 'msg>) =
            dropdownSingleImpl update activateOnHover icon selected attributes values


    /// Dropdown menu for a collection of values. Multiple items can be selected at a time.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The values can be provided as alist, amap, or sequence of keys with DOM nodes.
    let inline dropdownMultiSelect (update: 'T list -> 'msg) (activateOnHover: bool) (placeholder: string)
                                   (selected: alist<'T>) attributes values =
        let values: DropdownValues<'T, 'msg> = DropdownInternals.toDropdownValues values
        DropdownInternals.dropdownMultiSelect update activateOnHover placeholder selected (att attributes) values

    /// Dropdown menu for an enumeration type. Multiple items can be selected at a time.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The displayed values are derived from the enumeration value names, if the values argument is None.
    let inline dropdownEnumMultiSelect (update: 'T list -> 'msg) (activateOnHover: bool) (placeholder: string)
                                       (selected: alist<'T>) attributes values  =
        let values = DropdownInternals.getEnumValues<'T, _, 'msg> values |> DropdownInternals.toDropdownValues
        dropdownMultiSelect update activateOnHover placeholder selected attributes values

    /// Dropdown menu for a collection of values. At most a single item can be selected at a time.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The values can be provided as alist, amap, or sequence of keys with DOM nodes.
    let inline dropdownOption (update: 'T option -> 'msg) (activateOnHover: bool) (icon: string option) (placeholder: string)
                              (selected: aval<'T option>) attributes values =
        let values: DropdownValues<'T, 'msg> = DropdownInternals.toDropdownValues values
        DropdownInternals.dropdownOption update activateOnHover icon placeholder selected (att attributes) values

    /// Dropdown menu for an enumeration type. At most a single item can be selected at a time.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The displayed values are derived from the enumeration value names, if the values argument is None.
    let inline dropdownEnumOption (update: 'T option -> 'msg) (activateOnHover: bool) (icon: string option) (placeholder: string)
                                  (selected: aval<'T option>) attributes values =
        let values = DropdownInternals.getEnumValues<'T, _, 'msg> values
        dropdownOption update activateOnHover icon placeholder selected attributes values

    /// Dropdown menu for a collection of values.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The values can be provided as alist, amap, or sequence of keys with DOM nodes.
    let inline dropdown (update: 'T -> 'msg) (activateOnHover: bool) (icon: string option) (selected: aval<'T>) attributes values =
        let values: DropdownValues<'T, 'msg> = DropdownInternals.toDropdownValues values
        DropdownInternals.dropdown update activateOnHover icon selected (att attributes) values

    /// Dropdown menu for a an enumeration type.
    /// The attributes can be provided as AttributeMap, amap, alist, or sequence of (conditional) attributes.
    /// The displayed values are derived from the enumeration value names, if the values argument is None.
    let inline dropdownEnum (update: 'T -> 'msg) (activateOnHover: bool) (icon: string option) (selected: aval<'T>) attributes values =
        let values = DropdownInternals.getEnumValues<'T, _, 'msg> values
        dropdown update activateOnHover icon selected attributes values