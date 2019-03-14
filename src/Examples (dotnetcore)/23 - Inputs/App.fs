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
        | SetName n ->
            if model.active then
                { model with name = n }
            else
                model
            //if rand.NextDouble() > 0.5 then
            //    Log.warn "%A" (v - 2.0)
            //    { model with value = v - 2.0 }
            //else    
            //    Log.warn "%A" model.value
            //    model

let view (model : MModel) =
    div [clazz "ui inverted segment"; style "width: 100%; height: 100%"] [
        div [ clazz "ui vertical inverted menu" ] [
            div [ clazz "item" ] [ 
                checkbox [clazz "ui inverted checkbox"] model.active ToggleActive "Is the thing active?"
            ]
            div [ clazz "item" ] [ 
                checkbox [clazz "ui inverted toggle checkbox"] model.active ToggleActive "Is the thing active?"
            ]
            div [ clazz "item" ] [ 
                numeric { min = -1E15; max = 1E15; smallStep = 0.1; largeStep = 100.0 } [clazz "ui input"] model.value SetValue
            ]
            div [ clazz "item" ] [ 
                textbox { regex = Some "^[a-z]*$"; maxLength = Some 4 } [clazz "ui input"] model.name SetName
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
