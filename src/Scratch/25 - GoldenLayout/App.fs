module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        Inc -> { model with value = model.value + 1 }

let dependencies = [
    { name = "Golden"; url = "http://golden-layout.com/files/latest/js/goldenlayout.js"; kind = Script }
    { name = "GoldenCSS"; url = "http://golden-layout.com/files/latest/css/goldenlayout-base.css"; kind = Stylesheet }
    { name = "GoldenAard"; url = "./goldenAard.js"; kind = Stylesheet }
]

let view (model : AdaptiveModel) =
    require dependencies (
        onBoot "initLayout()" (
            div [] [
                text "Hello World"
                br []
                button [onClick (fun _ -> Inc)] [text "Increment"]
                text "    "
                Incremental.text (model.value |> AVal.map string)
                br []
                img [
                    attribute "src" "https://upload.wikimedia.org/wikipedia/commons/6/67/SanWild17.jpg"; 
                    attribute "alt" "aardvark"
                    style "max-width: 80%; max-height: 80%"
                ]
            ]
        )
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
