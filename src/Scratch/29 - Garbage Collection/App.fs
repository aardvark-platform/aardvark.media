module Input.App

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Input
open System


let initial = 
    { 
        items = [ "foo"; "bar" ]
    }

let rnd = new Random(0)

let update (model : Model) (msg : Message) =
    match msg with
        | Update ->
            { model with items = List.init 5 (fun _ -> rnd.Next().ToString() ) }
        | Nop -> model

let view (model : AdaptiveModel) =
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
