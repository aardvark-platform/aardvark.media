namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open Aardvark.UI.Generic

#nowarn "44" // TODO: Remove old dropdown stuff

[<AutoOpen>]
module SimplePrimitives =
    open System

    module internal Html =

        let semui =
            [
                { kind = Stylesheet; name = "semui"; url = "./resources/fomantic/semantic.css" }
                { kind = Script; name = "semui"; url = "./resources/fomantic/semantic.js" }
                { kind = Stylesheet; name = "semui-overrides"; url = "./resources/fomantic/semantic-overrides.css" }
                { kind = Script; name = "essential"; url = "./resources/essentialstuff.js" }
            ]


    [<ReferenceEquality; NoComparison>]
    type private Thing<'a> = { value : 'a }

    let inline private thing a = { value = a }

    type SliderConfig<'a> =
        {
            min         : 'a
            max         : 'a
            step        : 'a
        }

    type NumericConfig<'a> =
        {
            min         : 'a
            max         : 'a
            smallStep   : 'a
            largeStep   : 'a
        }


    type TextConfig =
        {
            regex       : Option<string>
            maxLength   : Option<int>
        }

    type TextAreaConfig =
        {
            placeholder : Option<string>
        }

    [<RequireQualifiedAccess>]
    type TriggerDropdown =
        | Hover
        | Click

    [<RequireQualifiedAccess>]
    type DropdownMode =
        /// Dropdown is represented by the icon of the given name.
        | Icon of iconName: string

        /// Dropdown is represented by the currently selected text entry.
        /// If a placeholder string is specified, it may be cleared by the user.
        | Text of placeholder: string option

        [<Obsolete("Use DropdownMode.Text with a placeholder.")>]
        static member Clearable(placeholder : string) =
            Text <| Some placeholder

        [<Obsolete("Use DropdownMode.Text without a placeholder.")>]
        static member Unclearable =
            Text None

    type DropdownConfig =
        {
            mode        : DropdownMode
            onTrigger   : TriggerDropdown
        }

    module DropdownConfig =

        /// Dropdown is represented by the icon of the given name.
        let icon (iconName : string) =
            { mode      = DropdownMode.Icon iconName
              onTrigger = TriggerDropdown.Click }

        /// Dropdown may be cleared by the user, displaying the given placeholder text.
        let clearable (placeholder : string) =
            { mode      = DropdownMode.Text (Some placeholder)
              onTrigger = TriggerDropdown.Click }

        /// Dropdown may not be cleared by the user.
        let unclearable =
            { mode      = DropdownMode.Text None
              onTrigger = TriggerDropdown.Click }

        [<Obsolete("Renamed to unclearable")>]
        let unClearable = unclearable

    type NumberType =
        | Int = 0
        | Real = 1
        | Decimal = 2

    type NumericConfigDefaults<'a>() =
        class
            [<DefaultValue>] static val mutable private min : 'a
            [<DefaultValue>] static val mutable private max : 'a
            [<DefaultValue>] static val mutable private small : 'a
            [<DefaultValue>] static val mutable private large : 'a
            [<DefaultValue>] static val mutable private numType : NumberType

            static member MinValue = NumericConfigDefaults<'a>.min
            static member MaxValue = NumericConfigDefaults<'a>.max
            static member SmallStep = NumericConfigDefaults<'a>.small
            static member LargeStep = NumericConfigDefaults<'a>.large
            static member NumType = NumericConfigDefaults<'a>.numType

            static member Get() =
                { min = NumericConfigDefaults<'a>.MinValue;
                  max = NumericConfigDefaults<'a>.MaxValue;
                  smallStep = NumericConfigDefaults<'a>.SmallStep;
                  largeStep = NumericConfigDefaults<'a>.LargeStep; }

            static do
                let at = typedefof<'a>
                if at = typeof<int> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> Int32.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> Int32.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10
                    NumericConfigDefaults<'a>.numType <- NumberType.Int
                elif at = typeof<uint32> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> UInt32.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> UInt32.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1u
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10u
                    NumericConfigDefaults<'a>.numType <- NumberType.Int
                elif at = typeof<float> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> Double.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> Double.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1.0
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10.0
                    NumericConfigDefaults<'a>.numType <- NumberType.Real
                elif at = typeof<float32> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> Single.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> Single.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1.0f
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10.0f
                    NumericConfigDefaults<'a>.numType <- NumberType.Real
                elif at = typeof<decimal> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> Decimal.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> Decimal.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1m
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10m
                    NumericConfigDefaults<'a>.numType <- NumberType.Decimal
                else
                    // user needs to fully specify min, max, small and large
                    NumericConfigDefaults<'a>.numType <- NumberType.Int
                    Log.warn "NumericConfigDefaults of type %A not implemented" typeof<'a>
                    //failwith "not supported type in NumericInput"
        end

    module TextConfig =
        let empty = { regex = None; maxLength = None }

    // TODO: Add [<AutoOpen>]
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

    module Incremental =

        [<AutoOpen>]
        module private Helpers =

            let inline pickle (v : 'a) = System.Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)

            let unpickle (v : string) =
                try
                    match NumericConfigDefaults<'a>.NumType with
                    | NumberType.Int ->
                        match System.Double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
                        | (true, v) -> let txt = Text(Math.Round(v).ToString())
                                       let rv = Text<'a>.Parse.Invoke(txt)
                                       Some rv
                        | _ -> None
                    | _ ->
                        let value = Text<'a>.Parse.Invoke(Text(v))
                        Some value
                with
                | _ ->
                    Log.warn "[UI.Primitives] failed to parse user input"
                    None

        let checkbox (atts : AttributeMap<'msg>) (state : aval<bool>) (toggle : 'msg) (l : list<DomNode<'msg>>) =
            let boot =
                String.concat "" [
                    "const $self = $('#__ID__');"
                    "$self.checkbox();"
                    "$self[0].addEventListener('click', function(e) { aardvark.processEvent('__ID__', 'onclick'); e.stopPropagation(); }, true);"
                    "isChecked.onmessage = function(s) { const behavior = s ? 'set checked' : 'set unchecked'; $self.checkbox(behavior); };"
                ]

            let myAtts =
                AttributeMap.ofList [
                    clazz "ui checkbox"
                    onClick (fun _ -> toggle)
                ]

            let atts = AttributeMap.union atts myAtts
            require Html.semui (
                onBoot' ["isChecked", AVal.channel state] boot (
                    Incremental.div atts <| AList.ofList [
                        yield input [attribute "type" "checkbox"]
                        match l with
                        | [l] ->
                            match l.NodeTag with
                            | Some "label" -> yield l
                            | _ -> yield label [] [l]
                        | _ ->
                            yield label [] l
                    ]
                )
            )

        let numeric' (cfg : NumericConfig<'a>) (atts : AttributeMap<'msg>)
                     (before : aval<DomNode<'msg> option>) (after : aval<DomNode<'msg> option>) (value : aval<'a>) (update : 'a -> #seq<'msg>) =

            let value = if value.IsConstant then AVal.custom (fun t -> value.GetValue t) else value

            let update (v : 'a) =
                value.MarkOutdated()
                update v

            let myAtts =
                AttributeMap.ofList [
                    "class", AttributeValue.String "ui input"
                    onEvent' "data-event" [] (function (str :: _) -> str |> unpickle |> Option.toList |> Seq.collect update | _ -> Seq.empty)
                ]

            let boot =
                String.concat ";" [
                    "var $__ID__ = $('#__ID__');"
                    "$__ID__.numeric({ changed: function(v) { aardvark.processEvent('__ID__', 'data-event', v); } });"
                    "valueCh.onmessage = function(v) { $__ID__.numeric('set', v.value); };"
                ]

            let pattern =
                match NumericConfigDefaults<'a>.NumType with
                | NumberType.Int -> "[0-9]+"
                | _ -> "[0-9]*(\.[0-9]*)?"

            require Html.semui (
                onBoot' ["valueCh", AVal.channel (AVal.map thing value)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
                            match! before with
                            | Some n -> yield n
                            | _ -> ()

                            yield
                                input (att [

                                    attribute "value" (value.GetValue() |> pickle)
                                    attribute "type" "text"
                                    attribute "min" (pickle cfg.min)
                                    attribute "max" (pickle cfg.max)
                                    attribute "step" (pickle cfg.smallStep)
                                    attribute "data-largestep" (pickle cfg.largeStep)
                                    //attribute "data-numtype" (NumericConfigDefaults<'a>.NumType.ToString())
                                    attribute "pattern" pattern
                                ])

                            match! after with
                            | Some n -> yield n
                            | _ -> ()
                        }
                    )
                )
            )

        let numeric (cfg : NumericConfig<'a>) (atts : AttributeMap<'msg>) (value : aval<'a>) (update : 'a -> 'msg) =
            numeric' cfg atts (AVal.constant None) (AVal.constant None) value (update >> List.singleton)

        let slider (cfg : SliderConfig<'a>) (atts : AttributeMap<'msg>) (value : aval<'a>) (update : 'a -> 'msg) =

            let value = if value.IsConstant then AVal.custom (fun t -> value.GetValue t) else value

            let update v =
                value.MarkOutdated()
                update v

            let myAtts =
                AttributeMap.ofList [
                    "class", AttributeValue.String "ui slider"
                    onEvent' "data-event" [] (function (str :: _) -> str |> unpickle |> Option.toList |> Seq.map update | _ -> Seq.empty)
                ]

            let boot =
                String.concat ";" [
                    "var $__ID__ = $('#__ID__');"
                    sprintf "var cfg = { decimalPlaces: 10, min: %s, max: %s, step: %s, start: %s, onMove: function(v) { aardvark.processEvent('__ID__', 'data-event', v);} };" (pickle cfg.min) (pickle cfg.max) (pickle cfg.step) (pickle (AVal.force value))
                    sprintf "$__ID__.slider(cfg);"
                    "valueCh.onmessage = function(v) { $__ID__.slider('update position', v.value); };"
                ]

            require Html.semui (
                onBoot' ["valueCh", AVal.channel (AVal.map thing value)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) AList.empty
                )
            )

        let textbox (cfg : TextConfig) (atts : AttributeMap<'msg>) (value : aval<string>) (update : string -> 'msg) =

            let value = if value.IsConstant then AVal.custom (fun t -> value.GetValue t) else value
            let update v =
                value.MarkOutdated()
                update v

            let myAtts =
                AttributeMap.ofList [
                    clazz "ui input"
                    onEvent' "data-event" [] (function (str :: _) -> Seq.delay (fun () -> Seq.singleton (update (Pickler.unpickleOfJson str))) | _ -> Seq.empty)
                ]

            let boot =
                String.concat ";" [
                    match cfg.regex with
                    | Some rx ->
                        yield "var validate = function(a) { if(/" + rx + "/.test(a)) { return a; } else { return null; }};"
                    | None ->
                        yield "var validate = function(a) { return a };"

                    yield "var $self = $('#__ID__');"
                    yield "var $input = $('#__ID__ > input');"
                    yield "var old = $input.val();"
                    yield "$input.on('input', function(e) { var v = validate(e.target.value); if(v) { $self.removeClass('error'); } else { $self.addClass('error'); } });"
                    yield "$input.change(function(e) { var v = validate(e.target.value); if(v) { old = v; aardvark.processEvent('__ID__', 'data-event', v); } else { $input.val(old); $self.removeClass('error'); } });"
                    yield "valueCh.onmessage = function(v) {  old = v.value; $input.val(v.value); };"
                ]

            require Html.semui (
                onBoot' ["valueCh", AVal.channel (AVal.map thing value)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
                            yield
                                input (att [
                                    match cfg.maxLength with
                                        | Some l when l > 0 -> yield attribute "maxlength" (string l)
                                        | _ -> ()
                                    yield attribute "value" (value.GetValue())
                                    yield attribute "type" "text"
                                ])
                        }
                    )
                )
            )

        let textarea (cfg : TextAreaConfig) (atts : AttributeMap<'msg>) (value : aval<string>) (update : string -> 'msg) =
            let value = if value.IsConstant then AVal.custom (fun t -> value.GetValue t) else value
            let update v =
                value.MarkOutdated()
                update v

            let myAtts =
                AttributeMap.ofList [
                    clazz "ui input"
                    onEvent' "data-event" [] (function (str :: _) -> Seq.delay (fun () -> Seq.singleton (update (Pickler.unpickleOfJson str))) | _ -> Seq.empty)
                ]

            let boot =
                String.concat ";" [
                    yield "var $self = $('#__ID__');"
                    yield "var $input = $('#__ID__ > textarea');"
                    yield "$input.change(function(e) {aardvark.processEvent('__ID__', 'data-event', e.target.value);});"
                    yield "valueCh.onmessage = function(v) { $input.val(v.value); };"
                ]

            require Html.semui (
                onBoot' ["valueCh", AVal.channel (AVal.map thing value)] boot (
                    Incremental.form (AttributeMap.union atts myAtts) (
                        alist {
                            yield
                                textarea (att [
                                    match cfg.placeholder with
                                    | Some ph when ph<>"" -> yield attribute "placeholder" ph
                                    | _ -> ()
                                    yield attribute "type" "text"
                                ]) value
                        }
                    )
                )
            )

        [<Obsolete("Use Dropdown.dropdownOption instead. Add 'clearable' as class attribute if input should be clearable. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
        let dropdown (cfg : DropdownConfig) (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<Option<'a>>) (update : Option<'a> -> 'msg) =
            let activateOnHover = cfg.onTrigger = TriggerDropdown.Hover

            match cfg.mode with
            | DropdownMode.Icon icon ->
                Dropdown.dropdownOption update activateOnHover (Some icon) "" selected atts values

            | DropdownMode.Text (Some placeholder) ->
                let atts = AttributeMap.union atts (AttributeMap.ofList [clazz "clearable"])
                Dropdown.dropdownOption update activateOnHover None placeholder selected atts values

            | DropdownMode.Text None ->
                Dropdown.dropdownOption update activateOnHover None "" selected atts values

        [<Obsolete("Use Dropdown.dropdown instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
        let dropdownUnclearable (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
            Dropdown.dropdown update false None selected atts values

        [<Obsolete("Use Dropdown.dropdown instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
        let dropdownUnClearable (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
            Dropdown.dropdown update false None selected atts values

        [<Obsolete("Use Dropdown.dropdownMultiSelect instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
        let dropdownMultiSelect (attributes : AttributeMap<'msg>)
                                (compare : Option<'T -> 'T -> int>) (defaultText : string)
                                (values : amap<'T, DomNode<'msg>>) (selected : alist<'T>) (update : 'T list -> 'msg) =
            match compare with
            | Some cmp ->
                let values = values |> AMap.toASet |> ASet.sortWith (fun (a, _) (b, _) -> cmp a b)
                Dropdown.dropdownMultiSelect update false defaultText selected attributes values
            | _ ->
                Dropdown.dropdownMultiSelect update false defaultText selected attributes values

        [<RequireQualifiedAccess>]
        type private AccordionInput<'msg> =
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

        let private accordionImpl (input: AccordionInput<'msg>) (attributes: AttributeMap<'msg>) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
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
                        let sections = Array.ofList sections

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
        let accordion' (toggle: int * bool -> 'msg) (active: aset<int>)
                       (attributes: AttributeMap<'msg>) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
            let cb s i = toggle (i, s)
            sections |> accordionImpl (AccordionInput.Multi (active, cb)) attributes

        /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
        /// The active value holds the index of the open section, or -1 if there is no open section.
        /// The setActive (index | -1) message is fired when a section is opened or closed.
        let accordionExclusive' (setActive: int -> 'msg) (active: aval<int>)
                                (attributes: AttributeMap<'msg>) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
            let cb s i = (if s then i else -1) |> setActive
            sections |> accordionImpl (AccordionInput.Single (active, cb)) attributes

        /// Simple container dividing content into titled sections, which can be opened and closed.
        /// If exclusive is true, only one section can be open at a time.
        let accordionSimple' (exclusive: bool) (attributes: AttributeMap<'msg>) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
            sections |> accordionImpl (AccordionInput.Empty exclusive) attributes

        /// Simple container dividing content into titled sections, which can be opened and closed.
        /// The active set holds the indices of the open sections.
        /// The toggle (index, isOpen) message is fired when a section is opened or closed.
        let accordion (toggle: int * bool -> 'msg) (active: aset<int>)
                      (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            sections |> List.map (fun (t, c) -> Incremental.text t, c) |> accordion' toggle active attributes

        /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
        /// The active value holds the index of the open section, or -1 if there is no open section.
        /// The setActive (index | -1) message is fired when a section is opened or closed.
        let accordionExclusive (setActive: int -> 'msg) (active: aval<int>)
                               (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            sections |> List.map (fun (t, c) -> Incremental.text t, c) |> accordionExclusive' setActive active attributes

        /// Simple container dividing content into titled sections, which can be opened and closed.
        /// If exclusive is true, only one section can be open at a time.
        let accordionSimple (exclusive: bool) (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            sections |> List.map (fun (t, c) -> Incremental.text t, c) |> accordionSimple' exclusive attributes

    [<AutoOpen>]
    module ``Primtive Builders`` =

        type CheckBuilder() =

            member inline x.Yield(()) =
                (AttributeMap.empty, [], (AVal.constant true, ()))

            [<CustomOperation("attributes")>]
            member inline x.Attributes((a,c,u), na) = (AttributeMap.union a (att na), c, u)

            [<CustomOperation("content")>]
            member inline x.Content((a,c,u), nc : list<DomNode<'msg>>) = (a, nc, u)

            [<CustomOperation("toggle")>]
            member inline x.Toggle((a,c,(v,m)), msg) = (a,c,(v,msg))

            [<CustomOperation("state")>]
            member inline x.State((a,c,(v,m)), vv) = (a,c,(vv, m))

            member inline x.Run((a,c,(v,msg))) =
                Incremental.checkbox a v msg c

        // To be replaced by NumericBuilder2
        type NumericBuilder<'a>() =

            member inline x.Yield(()) =
                (AttributeMap.empty, AVal.constant 0.0, { min = NumericConfigDefaults<'a>.MinValue; max = NumericConfigDefaults<'a>.MaxValue; smallStep = NumericConfigDefaults<'a>.SmallStep; largeStep = NumericConfigDefaults<'a>.LargeStep; }, (fun _ -> ()))

            [<CustomOperation("attributes")>]
            member inline x.Attributes((a,u,c,m), na) = (AttributeMap.union a (att na), u, c, m)

            [<CustomOperation("value")>]
            member inline x.Value((a,_,cfg,m), vv) = (a, vv, cfg, m)

            [<CustomOperation("update")>]
            member inline x.Update((a,v,cfg,_), msg) = (a,v, cfg, msg)

            [<CustomOperation("step")>]
            member inline x.Step((a,v,cfg,m), s) = (a,v, { cfg with NumericConfig.smallStep = s }, m)

            [<CustomOperation("largeStep")>]
            member inline x.LargeStep((a,v,cfg,m), s) = (a,v, { cfg with NumericConfig.largeStep = s }, m)

            [<CustomOperation("min")>]
            member inline x.Min((a,v,cfg,m), s) = (a,v, { cfg with NumericConfig.min = s }, m)

            [<CustomOperation("max")>]
            member inline x.Max((a,v,cfg,m), s) = (a,v, { cfg with NumericConfig.max = s }, m)


            member inline x.Run((a,v,cfg,msg)) =
                Incremental.numeric cfg a v msg

        type NumericBuilderState<'T, 'msg> =
            { attributes : AttributeMap<'msg>
              before     : aval<DomNode<'msg> option>
              after      : aval<DomNode<'msg> option>
              value      : aval<'T>
              config     : NumericConfig<'T>
              update     : 'T -> 'msg seq }

        type NumericBuilder2<'T>() =

            member inline x.Yield(()) : NumericBuilderState<'T, 'msg> =
                { attributes = AttributeMap.empty
                  before     = AVal.constant None
                  after      = AVal.constant None
                  value      = AVal.constant Unchecked.defaultof<'T>
                  config     = NumericConfigDefaults<'T>.Get()
                  update     = fun _ -> Seq.empty }

            [<CustomOperation("attributes")>]
            member inline x.Attributes(s : NumericBuilderState<'T, 'msg>, attributes) =
                { s with attributes = AttributeMap.union s.attributes (att attributes) }

            [<CustomOperation("value")>]
            member inline x.Value(s : NumericBuilderState<'T, 'msg>, value : aval<'T>) =
                { s with value = value }

            [<CustomOperation("update")>]
            member inline x.Update(s : NumericBuilderState<'T, 'msg>, update : 'T -> 'msg seq) =
                { s with update = update }

            [<CustomOperation("update")>]
            member inline x.Update(s : NumericBuilderState<'T, 'msg>, update : 'T -> 'msg list) =
                { s with update = update >> List.toSeq }

            [<CustomOperation("update")>]
            member inline x.Update(s : NumericBuilderState<'T, 'msg>, update : 'T -> 'msg array) =
                { s with update = update >> Array.toSeq }

            [<CustomOperation("update")>]
            member inline x.Update(s : NumericBuilderState<'T, 'msg>, update : 'T -> 'msg) =
                { s with update = update >> Seq.singleton }

            [<CustomOperation("step")>]
            member inline x.Step(s : NumericBuilderState<'T, 'msg>, step : 'T) =
                { s with config = { s.config with NumericConfig.smallStep = step } }

            [<CustomOperation("largeStep")>]
            member inline x.LargeStep(s : NumericBuilderState<'T, 'msg>, step : 'T) =
                { s with config = { s.config with NumericConfig.largeStep = step } }

            [<CustomOperation("min")>]
            member inline x.Min(s : NumericBuilderState<'T, 'msg>, min : 'T) =
                { s with config = { s.config with NumericConfig.min = min } }

            [<CustomOperation("max")>]
            member inline x.Max(s : NumericBuilderState<'T, 'msg>, max : 'T) =
                { s with config = { s.config with NumericConfig.max = max } }

            [<CustomOperation("before")>]
            member inline x.Before(s : NumericBuilderState<'T, 'msg>, before : aval<DomNode<'msg>>) =
                { s with before = before |> AVal.map Some }

            [<CustomOperation("before")>]
            member inline x.Before(s : NumericBuilderState<'T, 'msg>, before : DomNode<'msg>) =
                x.Before(s, AVal.constant before)

            [<CustomOperation("after")>]
            member inline x.After(s : NumericBuilderState<'T, 'msg>, after : aval<DomNode<'msg>>) =
                { s with after = after |> AVal.map Some }

            [<CustomOperation("after")>]
            member inline x.After(s : NumericBuilderState<'T, 'msg>, after : DomNode<'msg>) =
                x.After(s, AVal.constant after)

            member private x.Icon(s : NumericBuilderState<'T, 'msg>, icon : string, left : bool) =
                let node : DomNode<'msg> =
                    i [clazz $"{icon} icon"] []

                let s =
                    let atts = AttributeMap.ofList [clazz (if left then "left icon" else "icon")]
                    { s with attributes = AttributeMap.union s.attributes atts }

                if left then x.Before(s, node)
                else x.After(s, node)

            [<CustomOperation("iconLeft")>]
            member x.IconLeft(s : NumericBuilderState<'T, 'msg>, icon : string) =
                x.Icon(s, icon, true)

            [<CustomOperation("iconRight")>]
            member x.IconRight(s : NumericBuilderState<'T, 'msg>, icon : string) =
                x.Icon(s, icon, false)

            member private x.Label(s : NumericBuilderState<'T, 'msg>, classes : string, label : string, left : bool) =
                let node : DomNode<'msg> =
                    div [clazz $"ui {classes} label"] [ text label ]

                let s =
                    let atts = AttributeMap.ofList [clazz (if left then "labeled" else "right labeled")]
                    { s with attributes = AttributeMap.union s.attributes atts }

                if left then x.Before(s, node)
                else x.After(s, node)

            [<CustomOperation("labelLeft")>]
            member x.LabelLeft(s : NumericBuilderState<'T, 'msg>, classes : string, label : string) =
                x.Label(s, classes, label, true)

            [<CustomOperation("labelLeft")>]
            member inline x.LabelLeft(s : NumericBuilderState<'T, 'msg>, label : string) =
                x.LabelLeft(s, "", label)

            [<CustomOperation("labelRight")>]
            member x.LabelRight(s : NumericBuilderState<'T, 'msg>, classes : string, label : string) =
                x.Label(s, classes, label, false)

            [<CustomOperation("labelRight")>]
            member inline x.LabelRight(s : NumericBuilderState<'T, 'msg>, label : string) =
                x.LabelRight(s, "", label)

            member inline x.Run(s : NumericBuilderState<'T, 'msg>) : DomNode<'msg> =
                Incremental.numeric' s.config s.attributes s.before s.after s.value s.update

        type TextBuilder() =

            member inline x.Yield(()) =
                (AttributeMap.empty, AVal.constant "", { regex = None; maxLength = None }, (fun _ -> ()))

            [<CustomOperation("attributes")>]
            member inline x.Attributes((a,u,c,m), na) = (AttributeMap.union a (att na), u, c, m)

            [<CustomOperation("value")>]
            member inline x.Value((a,_,cfg,m), vv) = (a, vv, cfg, m)

            [<CustomOperation("update")>]
            member inline x.Update((a,v,cfg,_), msg) = (a,v, cfg, msg)



            [<CustomOperation("regex")>]
            member inline x.Regex((a,v,cfg,m), s) = (a,v, { cfg with regex = Some s }, m)


            [<CustomOperation("maxLength")>]
            member inline x.MaxLength((a,v,cfg,m), s) = (a,v, { cfg with maxLength = Some s }, m)

            member inline x.Run((a,v : aval<_>,cfg,msg)) =
                Incremental.textbox cfg a v msg

        [<Obsolete("To be removed.")>]
        type DropdownBuilder() =

            member inline x.Yield(()) =
                (AttributeMap.empty, AMap.empty, AVal.constant None, DropdownConfig.unclearable, (fun _ -> ()))

            [<CustomOperation("attributes")>]
            member inline x.Attributes((a,u,s,c,m), na) = (AttributeMap.union a (att na), u, s, c, m)

            [<CustomOperation("options")>]
            member inline x.Options((a,_,s,cfg,m), vv) = (a, vv, s, cfg, m)

            [<CustomOperation("value")>]
            member inline x.Value((a,v,_,cfg,m), s) = (a, v, s, cfg, m)

            [<CustomOperation("update")>]
            member inline x.Update((a,v,s,cfg,_), msg) = (a,v, s,cfg, msg)

            [<CustomOperation("mode")>]
            member inline x.Mode((a,v,s,cfg,m), e) = (a,v, s,{ cfg with mode = e }, m)

            [<CustomOperation("onTrigger")>]
            member inline x.OnTrigger((a,v,s,cfg,m), e) = (a,v, s,{ cfg with onTrigger = e }, m)

            member inline x.Run((a,v : amap<_,_>,s,cfg,msg)) =
                Incremental.dropdown cfg a v s msg

        let simplecheckbox = CheckBuilder()
        let simplenumeric<'a> = NumericBuilder<'a>()
        let simplenumeric'<'T> = NumericBuilder2<'T>()
        let simpletextbox = TextBuilder()

        [<Obsolete("To be removed.")>]
        let simpledropdown = DropdownBuilder()


    let inline checkbox atts (state : aval<bool>) (toggle : 'msg) content =
        Incremental.checkbox (att atts) state toggle [label [] content]

    let inline numeric (cfg : NumericConfig<'a>) atts (state : aval<'a>) (update : 'a -> 'msg) =
        Incremental.numeric cfg (att atts) state update

    let inline slider (cfg : SliderConfig<'a>) atts (state : aval<'a>) (update : 'a -> 'msg) =
        Incremental.slider cfg (att atts) state update

    let inline textbox (cfg : TextConfig) atts (state : aval<string>) (update : string -> 'msg) =
        Incremental.textbox cfg (att atts) state update

    let inline textarea (cfg : TextAreaConfig) atts (state : aval<string>) (update : string -> 'msg) =
        Incremental.textarea cfg (att atts) state update

    [<Obsolete("Use Dropdown.dropdownOption instead. Add 'clearable' as class attribute if input should be clearable. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
    let inline dropdown (cfg : DropdownConfig) atts (values : amap<'a, DomNode<'msg>>) (selected : aval<Option<'a>>) (update : Option<'a> -> 'msg) =
        Incremental.dropdown cfg (att atts) values selected update

    [<Obsolete("Use Dropdown.dropdown instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
    let inline dropdownUnclearable atts (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
        Incremental.dropdownUnclearable (att atts) values selected update

    [<Obsolete("Use Dropdown.dropdown instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
    let inline dropdownUnClearable atts (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
        Incremental.dropdownUnclearable (att atts) values selected update

    [<Obsolete("Use Dropdown.dropdownMultiSelect instead. Make sure to add the Aardvark.UI.Primitives WebPart.")>]
    let inline dropdownMultiSelect (attributes : AttributeMap<'msg>)
                                   (compare : Option<'T -> 'T -> int>) (defaultText : string)
                                   (values : amap<'T, DomNode<'msg>>) (selected : alist<'T>) (update : 'T list -> 'msg) =
        Incremental.dropdownMultiSelect attributes compare defaultText values selected update

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// The active set holds the indices of the open sections.
    /// The toggle message (index, isOpen) is fired when a section is opened or closed.
    let inline accordion' (toggle: int * bool -> 'msg) (active: aset<int>)
                          (attributes: Attribute<'msg> list) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> Incremental.accordion' toggle active attributes

    /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
    /// The active value holds the index of the open section, or -1 if there is no open section.
    /// The setActive (index | -1) message is fired when a section is opened or closed.
    let inline accordionExclusive' (setActive: int -> 'msg) (active: aval<int>)
                                   (attributes: Attribute<'msg> list) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> Incremental.accordionExclusive' setActive active attributes

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// If exclusive is true, only one section can be open at a time.
    let inline accordionSimple' (exclusive: bool) (attributes: Attribute<'msg> list) (sections: list<DomNode<'msg> * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> Incremental.accordionSimple' exclusive attributes

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// The active set holds the indices of the open sections.
    /// The toggle message (index, isOpen) is fired when a section is opened or closed.
    let inline accordion (toggle: int * bool -> 'msg) (active: aset<int>)
                         (attributes: Attribute<'msg> list) (sections: list<string * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> List.map (fun (t, n) -> AVal.constant t, n) |> Incremental.accordion toggle active attributes

    /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
    /// The active value holds the index of the open section, or -1 if there is no open section.
    /// The setActive (index | -1) message is fired when a section is opened or closed.
    let inline accordionExclusive (setActive: int -> 'msg) (active: aval<int>)
                                  (attributes: Attribute<'msg> list) (sections: list<string * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> List.map (fun (t, n) -> AVal.constant t, n) |> Incremental.accordionExclusive setActive active attributes

    /// Simple container dividing content into titled sections, which can be opened and closed.
    /// If exclusive is true, only one section can be open at a time.
    let inline accordionSimple (exclusive: bool) (attributes: Attribute<'msg> list) (sections: list<string * DomNode<'msg>>) =
        let attributes = AttributeMap.ofList attributes
        sections |> List.map (fun (t, n) -> AVal.constant t, n) |> Incremental.accordionSimple exclusive attributes