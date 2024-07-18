﻿namespace Aardvark.UI.Primitives

open FSharp.Data.Adaptive
open Aardvark.UI

type EmbeddedResources = EmbeddedResources
module Resources =
    open Suave
    let WebPart = Reflection.assemblyWebPart typeof<EmbeddedResources>.Assembly

[<AutoOpen>]
module Simple =
    open Microsoft.FSharp.Reflection

    let private uniqueClass str = "unique-" + str

    let integerInput (name : string) (minValue : int) (maxValue : int) (changed : int -> 'msg) (value : aval<int>) =
        let defaultValue = max 0 minValue
        let parse (str : string) =
            let str = str.Replace(".", "").Replace(",", "")
            match System.Int32.TryParse str with
                | (true, v) -> v
                | _ -> defaultValue

        let changed =
            AttributeValue.Event  {
                clientSide = fun send src -> 
                    String.concat "" [
                        "if(!event.inputType && event.target.value != event.target.oldValue) {"
                        "   event.target.oldValue = event.target.value;"
                        "   " + send src ["event.target.value"] + ";"
                        "}"
                    ]
                serverSide = fun client src args ->
                    match args with
                        | a :: _ -> 
                            let str : string = Pickler.unpickleOfJson a 
                            str |> int |> changed |> Seq.singleton
                        | _ -> Seq.empty
            }

        div [ clazz "ui input"; style "width: 60pt"] [
            Incremental.input <|
                AttributeMap.ofListCond [
                    yield always <| attribute "type" "number"
                    yield always <| attribute "step" "1"
                    if minValue > System.Int32.MinValue then yield always <| attribute "min" (string minValue)
                    if maxValue < System.Int32.MaxValue then yield always <| attribute "max" (string maxValue)

                    yield always <| attribute "placeholder" name
                    yield always <| attribute "size" "4"
                    
                    yield always <| ("oninput", changed)
                    yield always <| ("onchange", changed)

                    yield "value", value |> AVal.map (string >> AttributeValue.String >> Some)



                ]
        ]

    let labeledIntegerInput (name : string) (minValue : int) (maxValue : int) (changed : int -> 'msg) (value : aval<int>) =
        let defaultValue = max 0 minValue
        let parse (str : string) =
            let str = str.Replace(".", "").Replace(",", "")
            match System.Int32.TryParse str with
                | (true, v) -> v
                | _ -> defaultValue

        let changed =
            AttributeValue.Event  {
                clientSide = fun send src -> 
                    String.concat "" [
                        "if(!event.inputType && event.target.value != event.target.oldValue) {"
                        "   event.target.oldValue = event.target.value;"
                        "   " + send src ["event.target.value"] + ";"
                        "}"
                    ]
                serverSide = fun client src args ->
                    match args with
                        | a :: _ -> 
                            let str : string = Pickler.unpickleOfJson a 
                            str |> int |> changed |> Seq.singleton
                        | _ -> Seq.empty
            }

        div [ clazz "ui small labeled input"; style "width: 60pt"] [
            div [ clazz "ui label" ] [ text name ]
            Incremental.input <|
                AttributeMap.ofListCond [
                    yield always <| attribute "type" "number"
                    yield always <| attribute "step" "1"
                    if minValue > System.Int32.MinValue then yield always <| attribute "min" (string minValue)
                    if maxValue < System.Int32.MaxValue then yield always <| attribute "max" (string maxValue)

                    yield always <| attribute "placeholder" name
                    yield always <| attribute "size" "4"
                    
                    yield always <| ("oninput", changed)
                    yield always <| ("onchange", changed)

                    yield "value", value |> AVal.map (string >> AttributeValue.String >> Some)



                ]
        ]

    let labeledFloatInput'' (name : string) (minValue : float) (maxValue : float) (step : float) (changed : float -> 'msg) (value : aval<float>) (format : float -> string) (containerAttribs : AttributeMap<'msg>) (labelAttribs : AttributeMap<'msg>) =
        let defaultValue = max 0.0 minValue
        let parse (str : string) =
            match System.Double.TryParse str with
                | (true, v) -> v
                | _ -> defaultValue

        let changed =
            AttributeValue.Event  {
                clientSide = fun send src -> 
                    String.concat "" [
                        "if(!event.inputType && event.target.value != event.target.oldValue) {"
                        "   event.target.oldValue = event.target.value;"
                        "   " + send src ["event.target.value"] + ";"
                        "}"
                    ]
                serverSide = fun client src args ->
                    match args with
                        | a :: _ -> 
                            let str : string = Pickler.unpickleOfJson a 
                            str |> float |> changed |> Seq.singleton
                        | _ -> Seq.empty
            }

        Incremental.div containerAttribs <|
            AList.ofList [
                Incremental.div labelAttribs (AList.ofList [ text name ])
                Incremental.input <|
                    AttributeMap.ofListCond [
                        yield always <| attribute "type" "number"
                        yield always <| attribute "step" (string step)
                        yield always <| attribute "min" (string minValue)
                        yield always <| attribute "max" (string maxValue)

                        yield always <| attribute "placeholder" name
                        yield always <| attribute "size" "4"
                    
                        yield always <| ("oninput", changed)
                        yield always <| ("onchange", changed)

                        yield "value", value |> AVal.map (format >> AttributeValue.String >> Some)

                    ]
            ]

    let labeledFloatInput' (name : string) (minValue : float) (maxValue : float) (step : float) (changed : float -> 'msg) (value : aval<float>) (containerAttribs : AttributeMap<'msg>) (labelAttribs : AttributeMap<'msg>) =
        labeledFloatInput'' name minValue maxValue step changed value string containerAttribs labelAttribs

    let labeledFloatInput (name : string) (minValue : float) (maxValue : float) (step : float) (changed : float -> 'msg) (value : aval<float>) =
        labeledFloatInput' name minValue maxValue step changed value (AttributeMap.ofList [ clazz "ui small labeled input"; style "width: 60pt"]) (AttributeMap.ofList [ clazz "ui label" ]) 


    let modal (id : string) (name : string) (ok : 'msg) (attributes : list<string * AttributeValue<'msg>>) (content : list<DomNode<'msg>>) =
        require Html.semui (
            onBoot "$('#__ID__').modal();" (
                div [ yield clazz ("ui modal " + uniqueClass id); yield! attributes ] [
                    div [ clazz "header" ] [ text name ]
                    div [ clazz "content" ] content
                    div [ clazz "actions" ] [
                        div [ clazz "ui green approve button"; onClick (fun () -> ok) ] [text "OK"]
                        div [ clazz "ui red cancel button" ] [text "Cancel"]
                    ]
                ]
            )
        )

    let onClickModal (id : string) =
        let clazz = uniqueClass id
        clientEvent "onclick" (sprintf "$('.%s').modal('show');" clazz)

    let longString (len : int) (str : string) =
        if str.Length > len then
            str.Substring(0, len - 3) + "..."
        else
            str

    let largeTextArea' (changed : string -> 'msg) (value : aval<string>) (attributes : AttributeMap<'msg>) =

        let changed =
            AttributeValue.Event  {
                clientSide = fun send src -> send src ["event.target.value"] + ";"
                serverSide = fun client src args ->
                    match args with
                        | a :: _ -> 
                            let str : string = Pickler.unpickleOfJson a 
                            str.Trim('\"') |> changed |> Seq.singleton
                        | _ -> Seq.empty
            }

        let atts = 
            AttributeMap.union
                attributes
                (AttributeMap.ofListCond [
                    always <| ("oninput", changed)
                    "value", value |> AVal.map (AttributeValue.String >> Some)
                ])
        
        let req = 
            [
                { name = "metro-all.min.css"; url = "https://cdnjs.cloudflare.com/ajax/libs/metro/4.2.30/css/metro-all.min.css"; kind = Stylesheet }
                { name = "metro.min.js";  url = "https://cdnjs.cloudflare.com/ajax/libs/metro/4.2.30/js/metro.min.js";  kind = Script     }
            ]

        require req (Incremental.textarea atts (AVal.constant ""))

    let largeTextArea (changed : string -> 'msg) (value : aval<string>) =
        largeTextArea' changed value AttributeMap.empty