module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model

type WrappedMessage =
    | Wrapped of Message

let update (model : Model) (msg : WrappedMessage) =
    match msg with
        | Wrapped Increment ->
            { model with dummy = model.dummy + 1 }
        | Wrapped ResetAll ->
            { model with dummy = 0 }
        | Wrapped Ping ->
            model

module IncApp' =
    open Aardvark.Base.Incremental
    open Inc.App
    open Inc.Model

    let view (model : MModel) =
        div [] [
            text "Hello World"
            br []
            button [onClick (fun _ -> Inc)] [text "Increment"]
            text "    "
            Incremental.text (model.value |> Mod.map string)
            br []
            img [
                attribute "src" "https://upload.wikimedia.org/wikipedia/commons/6/67/SanWild17.jpg"; 
                attribute "alt" "aardvark"
                style "width: 200px"
            ]
        ]

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
                        IncApp'.app
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
            yield Wrapped Ping
            yield! inc()
        }
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty |> ThreadPool.add "inc" (inc())
        initial =  { dummy = 0 }
        update = update 
        view = view >> UI.map Wrapped
    }
