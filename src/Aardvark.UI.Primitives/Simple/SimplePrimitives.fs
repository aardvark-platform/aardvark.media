﻿namespace Aardvark.UI.Primitives

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

    let private semui =
        [
            { kind = Stylesheet; name = "semui"; url = "./rendering/semantic.css" }
            { kind = Stylesheet; name = "semui-overrides"; url = "./rendering/semantic-overrides.css" }
            { kind = Script; name = "semui"; url = "./rendering/semantic.js" }
            { kind = Script; name = "essential"; url = "./rendering/essentialstuff.js" }
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


    type DropdownConfig =
        {
            allowEmpty  : bool
            placeholder : string
        }

    type NumberType =
        | Int
        | Real
        | Decimal

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

            static do
                let at = typedefof<'a>
                if at = typeof<int> then
                    NumericConfigDefaults<'a>.min <- unbox<'a> Int32.MinValue
                    NumericConfigDefaults<'a>.max <- unbox<'a> Int32.MaxValue
                    NumericConfigDefaults<'a>.small <- unbox<'a> 1
                    NumericConfigDefaults<'a>.large <- unbox<'a> 10
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
                    | Int ->
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
            require semui (
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

        let numeric (cfg : NumericConfig<'a>) (atts : AttributeMap<'msg>) (value : aval<'a>) (update : 'a -> 'msg) =

            let value = if value.IsConstant then AVal.custom (fun t -> value.GetValue t) else value

            let update (v : 'a) =
                value.MarkOutdated()
                update v

            let myAtts =
                AttributeMap.ofList [
                    "class", AttributeValue.String "ui input"
                    onEvent' "data-event" [] (function (str :: _) -> str |> unpickle |> Option.toList |> Seq.map update | _ -> Seq.empty)
                ]

            let boot =
                String.concat ";" [
                    "var $__ID__ = $('#__ID__');"
                    "$__ID__.numeric({ changed: function(v) { aardvark.processEvent('__ID__', 'data-event', v); } });"
                    "valueCh.onmessage = function(v) { $__ID__.numeric('set', v.value); };"
                ]

            let pattern =
                match NumericConfigDefaults<'a>.NumType with
                | Int -> "[0-9]+"
                | _ -> "[0-9]*(\.[0-9]*)?"

            require semui (
                onBoot' ["valueCh", AVal.channel (AVal.map thing value)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
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
                        }
                    )
                )
            )

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

            require semui (
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

            require semui (
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

            require semui (
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


            let mutable id = 0

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
                | Some v -> sprintf ".dropdown('set selected', '%s');" v
                | None -> ".dropdown('clear');"


            let boot =
                let clear = if cfg.allowEmpty then "true" else "false"
                String.concat ";" [
                    "var $self = $('#__ID__');"
                    "$self.dropdown({ clearable: " + clear + ", onChange: function(value) {  debugger; aardvark.processEvent('__ID__', 'data-event', value); }, onHide : function() { var v = $self.dropdown('get value'); if(!v || v.length == 0) { $self.dropdown('clear'); } } })" + initial
                    "selectedCh.onmessage = function(value) { if(value.value) { $self.dropdown('set selected', value.value.Some); } else { $self.dropdown('clear'); } }; "
                ]

            require semui (
                onBoot' ["selectedCh", AVal.channel (AVal.map thing selection)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
                            yield input [ attribute "type" "hidden" ]
                            yield i [ clazz "dropdown icon" ] []
                            yield div [ clazz "default text"] cfg.placeholder
                            yield
                                Incremental.div (AttributeMap.ofList [clazz "menu"]) (
                                    alist {
                                        match compare with
                                        | Some cmp ->
                                            for (_, (value, node)) in valuesWithKeys |> AMap.toASet |> ASet.sortWith (fun (a,_) (b,_) -> cmp a b) do
                                                yield div [ clazz "item"; attribute "data-value" value] [node]
                                        | None ->
                                            for (_, (value, node)) in valuesWithKeys |> AMap.toASet |> ASet.sortBy (snd >> fst) do
                                                yield div [ clazz "item"; attribute "data-value" value] [node]
                                    }
                                )
                        }
                    )
                )
            )

        let dropdown1 (atts : AttributeMap<'msg>) (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
            dropdown { allowEmpty = false; placeholder = "" } atts values (AVal.map Some selected) (Option.get >> update)


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
                (AttributeMap.empty, AMap.empty, AVal.constant None, { allowEmpty = true; placeholder = "" }, (fun _ -> ()))

            [<CustomOperation("attributes")>]
            member inline x.Attributes((a,u,s,c,m), na) = (AttributeMap.union a (att na), u, s, c, m)

            [<CustomOperation("options")>]
            member inline x.Options((a,_,s,cfg,m), vv) = (a, vv, s, cfg, m)

            [<CustomOperation("value")>]
            member inline x.Value((a,v,_,cfg,m), s) = (a, v, s, cfg, m)

            [<CustomOperation("update")>]
            member inline x.Update((a,v,s,cfg,_), msg) = (a,v, s,cfg, msg)



            [<CustomOperation("allowEmpty")>]
            member inline x.AllowEmpty((a,v,s,cfg,m), e) = (a,v, s,{ cfg with allowEmpty = e }, m)


            [<CustomOperation("placeholder")>]
            member inline x.PlaceHolder((a,v,s,cfg,m), e) = (a,v, s,{ cfg with placeholder = e }, m)

            member inline x.Run((a,v : amap<_,_>,s,cfg,msg)) =
                Incremental.dropdown cfg a v s msg

        let simplecheckbox = CheckBuilder()
        let simplenumeric<'a> = NumericBuilder<'a>()
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

    let inline dropdown1 atts (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
        Incremental.dropdown1 (att atts) values selected update




