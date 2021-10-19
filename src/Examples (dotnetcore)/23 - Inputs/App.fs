module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Input
open System
open SortedHashMap
open NaturalOrder

let initial = 
    { 
        active = true
        value = Constant.Pi
        intValue = 13
        decValue = 1.1m
        uintValue = 1u
        name = "Pi"
        alt = Some A
        options = HashMap.ofList [A, "A"; B, "B"; C, "C";  D, "D"]
        enumValue = EnumValue.Value1
        itemSortedHelper = SortedHashMap<string, string * Alternative>.Empty(fun (v1,_) (v2,_) -> NaturalOrder.compare v1 v2)
        itemSortedList = IndexList.Empty
        itemSortedMap = HashMap.empty
        inputName = ""
        testHashMap = HashMap.ofList ["a", {value = 1.0}; "b", {value = 1.0}; "c", {value = 1.0};]
        brokenHashMap = HashMap.ofList ["a", 1.0; "b", 1.0; "c", 1.0 ]
    }


let rand = System.Random()

let updatedSortedStuff (h: SortedHashMap<_,_>) (model: Model) : Model = 
    { model with itemSortedHelper = h; itemSortedList = h.SortedKeys; itemSortedMap = h.Map }

let update (model : Model) (msg : Message) =
    match msg with
        | ToggleActive ->
            { model with active = not model.active }
            //if rand.NextDouble() > 0.5 then
            //    { model with active = not model.active }
            //else
            //    model
        | SetValue v ->
            if model.active then
                Log.warn "%A" v
                { model with value = v }
            else
                model
        | SetInt v -> Log.warn "SetInt :%d" v; { model with intValue = v }
        | SetDecimal v -> Log.warn "SetDecimal :%A" v; { model with decValue = v }
        | SetUInt v -> Log.warn "SetUInt :%A" v; { model with uintValue = v }
        | SetName n ->
            if model.active then
                { model with name = n; options = HashMap.add (Custom n) n model.options }
            else
                model
        | SetAlternative a ->
            if model.active then
                Log.warn "%A" a
                { model with alt = a }
            else 
                model
        | SetEnumValue v -> { model with enumValue = v }

        | SortInputName inputT -> { model with inputName = inputT }
        | SortAddSetItem (key, value, value2) -> 
            model |> updatedSortedStuff (model.itemSortedHelper.Add(key, (value,value2)))
        | SortUpdateSorting (key, v1) ->
            let _, otherValue = model.itemSortedHelper.Map |> HashMap.find key
            model |> updatedSortedStuff (model.itemSortedHelper.Add(key, (v1,otherValue)))
        | SortUpdateValue (key, v2) ->
            let otherValue,_  = model.itemSortedHelper.Map |> HashMap.find key
            model |> updatedSortedStuff (model.itemSortedHelper.Add(key, (otherValue, v2)))
        | SortRemoveItem key ->
            model |> updatedSortedStuff (model.itemSortedHelper.Remove key)

        | TestHashMapChange (key, v) -> 
            { model with testHashMap = model.testHashMap |> HashMap.alter key (Option.map (fun x -> { x with value = v }))}
        | BrokenHashMapChange (key, v) -> 
            { model with brokenHashMap = model.brokenHashMap |> HashMap.alter key (Option.map (fun x -> v))}

//let values =
//    AMap.ofList [
//        A, div [] [ text "A"; i [ clazz "icon rocket" ] []; i [ clazz "icon thermometer three quarters" ] [] ]
//        B, text "B"
//        C, text "C"
//        D, text "D"
//    ]

let enumValues = AMap.ofArray((Enum.GetValues typeof<EnumValue> :?> (EnumValue [])) |> Array.map (fun c -> (c, text (Enum.GetName(typeof<EnumValue>, c)) )))

let floatInputPostFix (placeholderName : string) (postFix:string) (minValue : float) (maxValue : float) (step : float) (changed : float -> 'msg) (value : aval<float>) (containerAttribs : AttributeMap<'msg>) =
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
            Incremental.input <|
                AttributeMap.ofListCond [
                    yield always <| attribute "type" "number"
                    yield always <| attribute "step" (string step)
                    yield always <| attribute "min" (string minValue)
                    yield always <| attribute "max" (string maxValue)

                    yield always <| attribute "placeholder" placeholderName
                    yield always <| attribute "size" "4"
        
                    yield always <| ("oninput", changed)
                    yield always <| ("onchange", changed)

                    yield "value", value |> AVal.map (string >> AttributeValue.String >> Some)

                ]
            text postFix
        ]

let buttonDisable (decission: aval<Option<'a>>) (iconName: string) (color: string) (action: 'a -> 'msg) =

    let selectionDeleteButton =
        amap {
            match! decission with
            | Some value -> 
                yield clazz (sprintf "ui icon inverted tiny button %s" color) 
                yield onClick (fun () -> (action value))
            | None ->
                yield clazz "ui icon inverted tiny disabled button "
        } |> AttributeMap.ofAMap

    Incremental.button selectionDeleteButton <| (AList.single (i [clazz (sprintf "%s icon" iconName)][]))

let myDropDown (values : amap<'a, DomNode<'msg>>) (selected : aval<'a>) (update : 'a -> 'msg) =
    SimplePrimitives.dropdown { allowEmpty = false; placeholder = "" } [clazz "ui inverted selection dropdown collapsing"; style "line-height: 0.4em"] values (AVal.map Some selected) (Option.get >> update)

type FlexJustifyContent = 
    | Start
    | End
    | CenterContent
    | SpaceBetween
    | SpaceAround
    | SpaceEvenly with 
        member x.Value : string = 
            match x with
            | Start         -> "flex-start"
            | End           -> "flex-end"
            | CenterContent -> "center"
            | SpaceBetween  -> "space-between"
            | SpaceAround   -> "space-around"
            | SpaceEvenly   -> "space-evenly"

type FlexItemAlignment = 
    | FlexStart
    | FlexEnd
    | Center
    | Strech
    | Baseline with 
        member x.Value : string = 
            match x with 
            | FlexStart     -> "flex-start"
            | FlexEnd       -> "flex-end"
            | Center        -> "center"
            | Strech        -> "strech"
            | Baseline      -> "baseline"

let divFlex' (attributes: list<Attribute<_>>) (justify: FlexJustifyContent) (alignment: FlexItemAlignment) (content: list<DomNode<'msg>>) : DomNode<'msg> = 
    let attr = [style (sprintf "display: flex; justify-content: %s; align-items:%s" justify.Value alignment.Value) ] @ attributes
    div attr content

let divFlex (justify: FlexJustifyContent) (alignment: FlexItemAlignment) (content: list<DomNode<'msg>>) : DomNode<'msg> = 
    divFlex' [] justify alignment content

let view (model : AdaptiveModel) =
    let values = model.options |> AMap.map (fun k v -> text v)
    div [clazz "ui inverted segment"; style "width: 100%; height: 100%"] [
        div [ clazz "ui vertical inverted menu" ] [
            div [ clazz "item" ] [ 
                simplecheckbox { 
                    attributes [clazz "ui inverted checkbox"]
                    state model.active
                    toggle ToggleActive
                    content [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ] 
                }
                //checkbox [clazz "ui inverted checkbox"] model.active ToggleActive [ text "Is the thing active?"; i [clazz "icon rocket" ] [] ]
            ]
            div [ clazz "item" ] [ 
                checkbox [clazz "ui inverted toggle checkbox"] model.active ToggleActive "Is the thing active?"
            ]
            div [ clazz "item" ] [ 
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.value
                    update SetValue
                    step 0.1
                    largeStep 1.0
                    min 1.0
                    max 100.0
                }
                //numeric { min = -1E15; max = 1E15; smallStep = 0.1; largeStep = 100.0 } [clazz "ui inverted input"] model.value SetValue
            ]
            div [ clazz "item" ] [ 
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.intValue
                    update SetInt
                    step 1
                    largeStep 5
                    min -100000
                    max 100000
                }
                //numeric { min = -1E15; max = 1E15; smallStep = 0.1; largeStep = 100.0 } [clazz "ui inverted input"] model.value SetValue
            ]
            div [ clazz "item" ] [ 
                // not using the simplenumeric builder
                numeric { min = 0; max = 10000; smallStep = 1; largeStep = 10 } [clazz "ui inverted input"] model.intValue SetInt
            ]
            div [ clazz "item" ] [ 
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.decValue
                    update SetDecimal
                    step 1m
                    largeStep 5m
                    min -100000m
                    max 100000m
                }
            ]
            div [ clazz "item" ] [ 
                simplenumeric {
                    attributes [clazz "ui inverted input"]
                    value model.uintValue
                    update SetUInt
                    step 1u
                    largeStep 5u
                    min 0u
                    max 100000u
                }
            ]
            div [ clazz "item" ] [ 
                slider { min = 1.0; max = 100.0; step = 0.1 } [clazz "ui inverted red slider"] model.value SetValue
            ]
            div [ clazz "item" ] [ 
                slider { min = 0; max = 20; step = 1 } [clazz "ui inverted blue slider"] model.intValue SetInt
            ]
            div [ clazz "item" ] [ 
                textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] model.name SetName
            ]
            div [ clazz "item" ] [ 
                dropdown { placeholder = "Thingy"; allowEmpty = false } [ clazz "ui inverted selection dropdown" ] values model.alt SetAlternative
            ]
            div [ clazz "item" ] [ 
                dropdown1 [ clazz "ui inverted selection dropdown" ] enumValues model.enumValue SetEnumValue
            ]
            div [ clazz "item "][
                text "fixed order and working focus"
                Incremental.div ([clazz "item"] |> AttributeMap.ofList) (model.testHashMap |> AMap.toASet |> ASet.toAList |> AList.map (fun (key, myNumber) ->
                    divFlex Start Center [
                        Incremental.text (myNumber.value |> AVal.map (sprintf "item: %.2f" ))
                        floatInputPostFix "" "value" 0.0 10.0 1.0 (fun v -> TestHashMapChange (key, v)) myNumber.value AttributeMap.empty
                    ]
                ))

                text "changing order and focus lost"
                Incremental.div ([clazz "item"] |> AttributeMap.ofList) (model.brokenHashMap |> AMap.toASet |> ASet.toAList |> AList.map (fun (key, myNumber) ->
                    divFlex Start Center [
                        text (sprintf "broken focus item: %.2f" myNumber)
                        floatInputPostFix "" "value" 0.0 10.0 1.0 (fun v -> BrokenHashMapChange (key, v)) (AVal.constant myNumber) AttributeMap.empty
                    ]
                ))
            ]
            div [ clazz "item" ][
                text "ADD / UPDATE items to sortable list"
                divFlex Start Center [
                    textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] model.inputName SortInputName
                    dropdown { placeholder = "value2"; allowEmpty = false } [ clazz "ui inverted selection dropdown" ] values model.alt SetAlternative
                    let validKey = adaptive {
                        let! keys = model.itemSortedMap |> AMap.keys |> ASet.toAVal
                        let! key = model.inputName
                        return 
                            match keys |> HashSet.contains key with
                            | true -> None
                            | false -> Some key
                        }
                    let validInput = (validKey, model.inputName, model.alt) |||> AVal.map3 (fun a b c -> match a,b,c with | ((Some key), v1, (Some v2)) when v1 <> "" -> Some (key,v1,v2) | _ -> None)
                    buttonDisable validInput "add" "green" SortAddSetItem
                    let overwriteEntry = adaptive {
                        let! keys = model.itemSortedMap |> AMap.keys |> ASet.toAVal
                        let! key = model.inputName
                        return 
                            match keys |> HashSet.contains key with
                            | false -> None
                            | true -> Some key
                        }
                    let validInput2 = (overwriteEntry, model.inputName, model.alt) |||> AVal.map3 (fun a b c -> match a,b,c with | ((Some key), v1, (Some v2)) when v1 <> "" -> Some (key,v1,v2) | _ -> None)
                    buttonDisable validInput2 "circle" "orange" SortAddSetItem
                ]
                text "DYNAMIC SORTABLE list where order is given by item-name"
                Incremental.div ([clazz "item"] |> AttributeMap.ofList) (model.itemSortedList |> AList.map (fun key ->
                    let item = model.itemSortedMap |> AMap.find key
                    divFlex Start Center [
                        text (sprintf "key: %s | sorting: " key)
                        textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] (item |> AVal.map fst) (fun v -> SortUpdateSorting(key, v))
                        text "value: "
                        myDropDown values (item |> AVal.map snd) (fun v -> SortUpdateValue (key, v))
                        button [onClick (fun _ -> SortRemoveItem key)][text "RemoveItem"]
                    ]
                ))
            ]
        ]
    ]

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty 
        initial = initial
        update = update 
        view = view
    }
