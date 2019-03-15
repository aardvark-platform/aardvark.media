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
        name = "Pi"
        alt = Some A
        options = HMap.ofList [A, "A"; B, "B"; C, "C";  D, "D"]
    }


let rand = System.Random()

let update (model : Model) (msg : Message) =
    match msg with
        | ToggleActive ->
            { model with active = not model.active; alt = if model.active then None else model.alt }
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
        | SetName n ->
            if model.active then
                { model with name = n; options = HMap.add (Custom n) n model.options }
            else
                model
        | SetAlternative a ->
            Log.warn "%A" a
            { model with alt = a }

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
                checkbox [clazz "ui inverted checkbox"] model.active ToggleActive "Is the thing active?"
            ]
            div [ clazz "item" ] [ 
                checkbox [clazz "ui inverted toggle checkbox"] model.active ToggleActive "Is the thing active?"
            ]
            div [ clazz "item" ] [ 
                numeric { min = -1E15; max = 1E15; smallStep = 0.1; largeStep = 100.0 } [clazz "ui inverted input"] model.value SetValue
            ]
            div [ clazz "item" ] [ 
                textbox { regex = Some "^[a-z]*$"; maxLength = Some 4 } [clazz "ui inverted input"] model.name SetName
            ]
            div [ clazz "item" ] [ 
                dropdown { placeholder = "Alternative"; allowEmpty = false } [ clazz "ui inverted selection dropdown" ] values model.alt SetAlternative
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
