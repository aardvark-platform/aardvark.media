module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Input


let initial = 
    { 
        active = true
        value = Constant.Pi
        intValue = 13
        decValue = 1.1m
        uintValue = 1u
        name = "Pi"
        alt = Some A
        options = HMap.ofList [A, "A"; B, "B"; C, "C";  D, "D"]
    }


let rand = System.Random()

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
                { model with name = n; options = HMap.add (Custom n) n model.options }
            else
                model
        | SetAlternative a ->
            if model.active then
                Log.warn "%A" a
                { model with alt = a }
            else 
                model
//let values =
//    AMap.ofList [
//        A, div [] [ text "A"; i [ clazz "icon rocket" ] []; i [ clazz "icon thermometer three quarters" ] [] ]
//        B, text "B"
//        C, text "C"
//        D, text "D"
//    ]

let view (model : MModel) =
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
                textbox { regex = Some "^[a-zA-Z_]+$"; maxLength = Some 6 } [clazz "ui inverted input"] model.name SetName
            ]
            div [ clazz "item" ] [ 
                dropdown { placeholder = "Thingy"; allowEmpty = false } [ clazz "ui inverted selection dropdown" ] values model.alt SetAlternative
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
