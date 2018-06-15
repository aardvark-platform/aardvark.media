module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let update (model : Model) (msg : Message) =
    { model with dummy = model.dummy + 1 }

let view (model : MModel) =
    body [] [
        div [style "display: flex; flex-direction: column; width: 100%; height: 100%"] [
            div [] [
                Incremental.text (model.dummy |> Mod.map (sprintf "messages: %d"))
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' (fun _model _innermsg -> Seq.singleton Increment) [] Inc.App.app
                ]
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' (fun _model _innermsg -> Seq.singleton Increment) [] RenderControl.App.app
                ]
            ]
        ]
    ]


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty 
        initial =  { dummy = 0 }
        update = update 
        view = view
    }
