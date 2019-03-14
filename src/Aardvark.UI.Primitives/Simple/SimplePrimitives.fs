namespace Aardvark.UI.Primitives

open Aardvark.UI
open Aardvark.Base.Incremental
open Aardvark.UI.Generic

[<AutoOpen>]
module SimplePrimitives =

    let private bootCheckBox =
        String.concat "" [
            "$('#__ID__').checkbox().checkbox('__INITIALSTATE__');"
            "$('#__ID__').get(0).addEventListener('click', function(e) { aardvark.processEvent('__ID__', 'onclick'); e.stopPropagation(); }, true);"
            "isChecked.onmessage = function(s) { if (s) { $('#__ID__').checkbox('check'); } else { $('#__ID__').checkbox('uncheck'); } };"
        ]
        
    let private semui = 
        [ 
            { kind = Stylesheet; name = "semui"; url = "./rendering/semantic.css" }
            { kind = Script; name = "semui"; url = "./rendering/semantic.js" }
            { kind = Script; name = "essential"; url = "./rendering/essentialstuff.js" }
        ]      

        
    [<ReferenceEquality; NoComparison>]
    type private Thing<'a> = { value : 'a }

    let inline private thing a = { value = a }


    type NumericConfig =
        {
            min         : float
            max         : float
            smallStep   : float
            largeStep   : float
        }

    type TextConfig =
        {
            regex       : Option<string>
            maxLength   : Option<int>
        }

    module TextConfig =
        let empty = { regex = None; maxLength = None }


    module Incremental =
    
        let checkbox (atts : AttributeMap<'msg>) (state : IMod<bool>) (toggle : 'msg) (l : list<DomNode<'msg>>) =
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
                onBoot' ["isChecked", Mod.channel state] boot (
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



        let numeric (cfg : NumericConfig) (atts : AttributeMap<'msg>) (value : IMod<float>) (update : float -> 'msg) =
            let inline pickle (v : float) = v.ToString(System.Globalization.CultureInfo.InvariantCulture)
            
            let inline unpickle (v : string) = 
                match System.Double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
                | (true, v) -> Some v
                | _ -> None

            let update v =
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
                
            require semui (
                onBoot' ["valueCh", Mod.channel (Mod.map thing value)] boot (
                    Incremental.div (AttributeMap.union atts myAtts) (
                        alist {
                            yield 
                                input (att [ 
                                    
                                    attribute "value" (value.GetValue() |> sprintf "%f")
                                    attribute "type" "text"
                                    attribute "min" (pickle cfg.min)
                                    attribute "max" (pickle cfg.max)
                                    attribute "step" (pickle cfg.smallStep) 
                                    attribute "data-largestep" (pickle (cfg.largeStep / cfg.smallStep))
                                    attribute "pattern" "[0-9]*(\.[0-9]*)?"
                                ])
                        }
                    )
                )
            )


        let textbox (cfg : TextConfig) (atts : AttributeMap<'msg>) (value : IMod<string>) (update : string -> 'msg) =
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
                        yield "var validate = function(a) { return a });"

                    yield "var $__ID__ = $('#__ID__ > input');"
                    yield "var old = $__ID__.val();"
                    yield "$__ID__.change(function(e) { var v = validate(e.target.value); if(v) { old = v; aardvark.processEvent('__ID__', 'data-event', v); } else { $__ID__.val(old); } });"
                    yield "valueCh.onmessage = function(v) {  old = v.value; $__ID__.val(v.value); };"
                ]
                       
            require semui (
                onBoot' ["valueCh", Mod.channel (Mod.map thing value)] boot (
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


    let inline checkbox atts (state : IMod<bool>) (toggle : 'msg) content =
        Incremental.checkbox (att atts) state toggle [label [] content]
        
    let inline numeric (cfg : NumericConfig) atts (state : IMod<float>) (update : float -> 'msg) =
        Incremental.numeric cfg (att atts) state update 

    let inline textbox (cfg : TextConfig) atts (state : IMod<string>) (update : string -> 'msg) =
        Incremental.textbox cfg (att atts) state update 



