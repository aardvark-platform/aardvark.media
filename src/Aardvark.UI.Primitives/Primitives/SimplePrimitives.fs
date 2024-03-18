namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open Aardvark.UI.Generic

[<AutoOpen>]
module SimplePrimitives =
    open System


    let private bootCheckBox =
        String.concat "" [
            "$('#__ID__').checkbox().checkbox('__INITIALSTATE__');"
            "$('#__ID__').get(0).addEventListener('click', function(e) { aardvark.processEvent('__ID__', 'onclick'); e.stopPropagation(); }, true);"
            "isChecked.onmessage = function(s) { if (s) { $('#__ID__').checkbox('check'); } else { $('#__ID__').checkbox('uncheck'); } };"
        ]
        
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
            let state = if state.IsConstant then AVal.custom (fun t -> state.GetValue t) else state

            let ev =
                {
                    clientSide = fun _ _ -> ""
                    serverSide = fun _ _ _ -> Seq.singleton toggle
                }

            let boot = bootCheckBox.Replace("__INITIALSTATE__", if state.GetValue() then "check" else "uncheck")

            let myAtts =
                AttributeMap.ofList [
                    "onclick", AttributeValue.Event ev
                    "class", AttributeValue.String "ui checkbox"
                ]

            let atts = AttributeMap.union atts myAtts
            require Html.semui (
                onBoot' ["isChecked", AVal.channel state] boot (
                    Incremental.div atts (
                        alist {
                            yield input [attribute "type" "checkbox"]
                            match l with
                            | [l] ->
                                match l.NodeTag with
                                | Some "label" -> yield l
                                | _ -> yield label [] [l]
                            | _ ->
                                yield label [] l
                        }
                    )
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

        let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

        let dropdown (cfg : DropdownConfig) (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<Option<'a>>) (update : Option<'a> -> 'msg) =

            let selected = if selected.IsConstant then AVal.custom (fun t -> selected.GetValue t) else selected

            let compare =
                if typeof<System.IComparable>.IsAssignableFrom typeof<'a> then Some Unchecked.compare<'a>
                else None

            let valuesWithKeys =
                values
                |> AMap.map (fun k v ->
                    let hash = pickler.ComputeHash(k).Hash |> System.Convert.ToBase64String
                    //let hash = System.Threading.Interlocked.Increment(&id) |> string
                    hash, v
                )

            let m =
                valuesWithKeys
                |> AMap.toAVal
                |> AVal.map (HashMap.map (fun k (v,_) -> v))
                |> AVal.map (fun m -> m, HashMap.ofSeq (Seq.map (fun (a,b) -> b,a) m))

            let update (k : string) =
                let _fw, bw = AVal.force m
                selected.MarkOutdated()
                try
                    match HashMap.tryFind k bw with
                    | Some v -> update (Some v) |> Seq.singleton
                    | None -> update None |> Seq.singleton
                with _ ->
                    Seq.empty

            let selection =
                selected |> AVal.bind (function Some v -> m |> AVal.map (fun (fw,_) -> HashMap.tryFind v fw) | None -> AVal.constant None)

            let myAtts =
                AttributeMap.ofList [
                    clazz "ui dropdown"
                    onEvent' "data-event" [] (function (str :: _) -> Seq.delay (fun () -> update (Pickler.unpickleOfJson str)) | _ -> Seq.empty)
                ]

            let initial =
                match selection.GetValue() with
                | Some v -> sprintf ".dropdown('set selected', '%s', '', true);" v
                | None -> ".dropdown('clear');"

            let boot =
                let trigger =
                    match cfg.onTrigger with
                    | TriggerDropdown.Click -> "'click'" // default
                    | TriggerDropdown.Hover -> "'hover'"

                let clearable =
                    match cfg.mode with
                    | DropdownMode.Text (Some _) -> "true"
                    |  _ -> "false"

                String.concat ";" [
                    "var $self = $('#__ID__');"
                    "$self.dropdown({ on: " + trigger + ", clearable: " + clearable + ", onChange: function(value) {  aardvark.processEvent('__ID__', 'data-event', value); }, onHide : function() { var v = $self.dropdown('get value'); if(!v || v.length == 0) { $self.dropdown('clear'); } } })" + initial
                    "selectedCh.onmessage = function(value) { if(value.value) { $self.dropdown('set selected', value.value.Some, '', true); } else { $self.dropdown('clear'); } }; "
                ]

            require Html.semui (
                onBoot' ["selectedCh", AVal.channel (AVal.map thing selection)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
                            yield input [ attribute "type" "hidden" ]
                            match cfg.mode with
                            | DropdownMode.Text ph ->
                                let ph = ph |> Option.defaultValue ""
                                yield i [ clazz "dropdown icon" ] []
                                yield div [ clazz "default text"] ph
                            | DropdownMode.Icon iconName ->
                                yield i [ clazz (sprintf "%s icon" iconName)] []
                            yield
                                Incremental.div (AttributeMap.ofList [clazz "ui menu"]) (
                                    alist {
                                        match compare with
                                        | Some cmp ->
                                            for (_, (value, node)) in valuesWithKeys |> AMap.toASet |> ASet.sortWith (fun (a,_) (b,_) -> cmp a b) do
                                                yield div [ clazz "ui item"; attribute "data-value" value] [node]
                                        | None ->
                                            for (_, (value, node)) in valuesWithKeys |> AMap.toASet |> ASet.sortBy (snd >> fst) do
                                                yield div [ clazz "ui item"; attribute "data-value" value] [node]
                                    }
                                )
                        }
                    )
                )
            )

        let dropdownUnclearable (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
            dropdown DropdownConfig.unclearable atts values (AVal.map Some selected) (Option.get >> update)

        [<Obsolete("Renamed to dropdownUnclearable")>]
        let dropdownUnClearable (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
            dropdown DropdownConfig.unclearable atts values (AVal.map Some selected) (Option.get >> update)

        let dropdownMultiSelect (attributes : AttributeMap<'msg>)
                                (compare : Option<'T -> 'T -> int>) (defaultText : string)
                                (values : amap<'T, DomNode<'msg>>) (selected : alist<'T>) (update : 'T list -> 'msg) =
            let valuesWithKeys =
                values
                |> AMap.map (fun k v ->
                    let hash = pickler.ComputeHash(k).Hash |> Convert.ToBase64String
                    hash, v
                )

            let lookup =
                valuesWithKeys
                |> AMap.toAVal
                |> AVal.map (HashMap.map (fun k (v,_) -> v))
                |> AVal.map (fun m -> m, HashMap.ofSeq (Seq.map (fun (a,b) -> b,a) m))

            let items =
                let set = valuesWithKeys |> AMap.toASet

                let compare =
                    compare |> Option.orElse (
                        if typeof<IComparable>.IsAssignableFrom typeof<'T> then
                            Some Unchecked.compare<'T>
                        else
                            None
                    )

                match compare with
                | Some cmp ->
                    set |> ASet.sortWith (fun (a,_) (b,_) -> cmp a b)
                | _ ->
                    set |> ASet.sortBy (snd >> fst)

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
                    Log.warn "[dropdownMultiSelect] callback failed: %s" exn.Message
                    Seq.empty

            let selection =
                adaptive {
                    let! selected = selected |> AList.toAVal
                    let! (fw, _) = lookup

                    return selected
                        |> IndexList.choose (fun v -> HashMap.tryFind v fw)
                        |> IndexList.toList
                }

            let attributes =
                AttributeMap.ofList [
                    clazz "ui dropdown multiple selection"
                    onEvent' "data-event" [] update
                ]
                |> AttributeMap.union attributes

            let boot =
                String.concat "" [
                    "const $self = $('#__ID__');"

                    "$self.dropdown({"
                    "    onChange: function(value) { aardvark.processEvent('__ID__', 'data-event', value); },"
                    "});"

                    // God I hate JS...
                    // https://stackoverflow.com/questions/7837456/how-to-compare-arrays-in-javascript/7837725#7837725
                    "function arraysIdentical(a, b) {"
                    "    var i = a.length;"
                    "    if (i != b.length) return false;"
                    "    while (i--) {"
                    "        if (a[i] !== b[i]) return false;"
                    "    }"
                    "    return true;"
                    "};"

                    "selectedCh.onmessage = function(values) {"
                    "    const curr = $self.dropdown('get values');"

                         // Prevent resetting the same values (leads to flickering)
                    "    if (arraysIdentical(curr, values)) {"
                    "        return;"
                    "    }"

                    "    $self.dropdown('clear', true);"
                    "    $self.dropdown('set selected', values, true);"  // set exactly bugged? clear seems to trigger event
                    "};"
                ]

            require Html.semui (
                onBoot' ["selectedCh", AVal.channel selection] boot (
                    Incremental.div attributes <| AList.ofList [
                        input [ attribute "type" "hidden" ]

                        i [ clazz "dropdown icon" ] []
                        div [ clazz "default text"] defaultText

                        Incremental.div (AttributeMap.ofList [clazz "ui menu"]) <| alist {
                            for (_, (value, node)) in items do
                                yield div [ clazz "ui item"; attribute "data-value" value] [node]
                        }
                    ]
                )
            )

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

        let private accordionImpl (input: AccordionInput<'msg>) (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
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

            require dependencies (
                onBoot' channels boot (
                    Incremental.div attributes <| AList.ofList [
                        for (title, node) in sections do
                            div [clazz "title"] [
                                i [clazz "dropdown icon"] []
                                Incremental.text title
                            ]
                            div [clazz "content"] [
                                node
                            ]
                    ]
                )
            )

        /// Simple container dividing content into titled sections, which can be opened and closed.
        /// The active set holds the indices of the open sections.
        /// The toggle (index, isOpen) message is fired when a section is opened or closed.
        let accordion (toggle: int * bool -> 'msg) (active: aset<int>)
                      (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            let cb s i = toggle (i, s)
            sections |> accordionImpl (AccordionInput.Multi (active, cb)) attributes

        /// Simple container dividing content into titled sections, which can be opened and closed (only one can be open at a time).
        /// The active value holds the index of the open section, or -1 if there is no open section.
        /// The setActive (index | -1) message is fired when a section is opened or closed.
        let accordionExclusive (setActive: int -> 'msg) (active: aval<int>)
                               (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            let cb s i = (if s then i else -1) |> setActive
            sections |> accordionImpl (AccordionInput.Single (active, cb)) attributes

        /// Simple container dividing content into titled sections, which can be opened and closed.
        /// If exclusive is true, only one section can be open at a time.
        let accordionSimple (exclusive: bool) (attributes: AttributeMap<'msg>) (sections: list<aval<string> * DomNode<'msg>>) =
            sections |> accordionImpl (AccordionInput.Empty exclusive) attributes

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

    let inline dropdown (cfg : DropdownConfig) atts (values : amap<'a, DomNode<'msg>>) (selected : aval<Option<'a>>) (update : Option<'a> -> 'msg) =
        Incremental.dropdown cfg (att atts) values selected update

    let inline dropdownUnclearable atts (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
        Incremental.dropdownUnclearable (att atts) values selected update

    [<Obsolete("Renamed to dropdownUnclearable")>]
    let inline dropdownUnClearable atts (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
        Incremental.dropdownUnclearable (att atts) values selected update

    let inline dropdownMultiSelect (attributes : AttributeMap<'msg>)
                                   (compare : Option<'T -> 'T -> int>) (defaultText : string)
                                   (values : amap<'T, DomNode<'msg>>) (selected : alist<'T>) (update : 'T list -> 'msg) =
        Incremental.dropdownMultiSelect attributes compare defaultText values selected update

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