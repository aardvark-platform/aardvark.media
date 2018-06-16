module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let update (model : Model) (msg : Message) =
    match msg with
        | Increment ->
            { model with dummy = model.dummy + 1 }
        | ResetAll ->
            { model with dummy = 0 }


let view (model : MModel) =
    body [] [
        div [style "display: flex; flex-direction: column; width: 100%; height: 100%"] [
            div [] [
                Incremental.text (model.dummy |> Mod.map (sprintf "messages: %d"))
                
                button [ onClick (fun () -> ResetAll) ] [ text "Reset" ]
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' (fun _model _innermsg -> Seq.singleton Increment) (function ResetAll -> Seq.singleton Inc.Model.Message.Inc | _ -> Seq.empty) [] Inc.App.app
                ]
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' (fun _model _innermsg -> Seq.singleton Increment) (function ResetAll -> Seq.singleton RenderControl.Model.Message.CenterScene | _ -> Seq.empty) [] RenderControl.App.app
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
