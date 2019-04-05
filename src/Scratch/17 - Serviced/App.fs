module Inc.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        Inc -> { model with value = model.value + 1 }

let menu () =
    div [clazz "menu-bar"] [
        div [ clazz "ui inverted top attached mini menu"; style "z-index: 1000"] [
            onBoot "$('#__ID__').dropdown({on: 'hover'});" (
                div [ clazz "ui inverted dropdown item" ] [
                    text "File"
                                
                    div [ clazz "ui inverted mini menu" ] [
                        div [ clazz "ui inverted item";  ] [
                            text "Import Volume"
                        ]
                        div [ clazz "ui inverted item"; ] [
                            text "Segmentation"
                        ]
                        div [ clazz "ui inverted item";  ] [
                            text "Merge filtered"
                        ]
                    ] 
                ]
            )
        ]
    ]

let view (model : MModel) =
    require Html.semui (
        body [style "background: rgb(27, 28, 29);"] [
            menu ()
        ]
    )


let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               value = 0
            }
        update = update 
        view = view
    }
