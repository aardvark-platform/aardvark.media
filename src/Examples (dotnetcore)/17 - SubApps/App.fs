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
        | Ping ->
            model

let view (model : MModel) =
    body [] [
        div [style "display: flex; flex-direction: column; width: 100%; height: 100%"] [
            div [] [
                Incremental.text (model.dummy |> Mod.map (sprintf "messages: %d"))
                
                button [ onClick (fun () -> ResetAll) ] [ text "Reset" ]
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' 
                        (fun _model _innermsg -> Seq.singleton Increment) 
                        (fun _model msg ->
                            match msg with
                                | ResetAll -> Seq.singleton Inc.Model.Message.Inc 
                                | Ping -> Log.warn "ping inc"; Seq.empty
                                | _ -> Seq.empty
                        ) 
                        [] 
                        Inc.App.app
                ]
            ]

            div [ style "display: flex; height: 40%" ] [
                div [style "position: absolute" ] [
                    subApp' 
                        (fun _model _innermsg -> Seq.singleton Increment) 
                        (fun _model msg ->
                            match msg with
                                | ResetAll -> Seq.singleton RenderControl.Model.Message.CenterScene 
                                | Ping -> Log.warn "ping rc"; Seq.empty
                                | _ -> Seq.empty
                        ) 
                        [] RenderControl.App.app
                ]
            ]
        ]
    ]


let app =                  
    let rec inc() =
        proclist {
            do! Async.Sleep 2000
            yield Ping
            yield! inc()
        }
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty |> ThreadPool.add "inc" (inc())
        initial =  { dummy = 0 }
        update = update 
        view = view
    }
