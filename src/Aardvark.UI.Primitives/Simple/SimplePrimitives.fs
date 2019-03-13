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
        ]      

    module Incremental =
    
        let checkBox (atts : AttributeMap<'msg>) (state : IMod<bool>) (toggle : 'msg) (l : list<DomNode<'msg>>) =
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

    let inline checkBox atts (state : IMod<bool>) (toggle : 'msg) content =
        Incremental.checkBox (att atts) state toggle [label [] content]


