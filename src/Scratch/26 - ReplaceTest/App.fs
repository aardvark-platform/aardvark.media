module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        Inc ->    Config.shouldPrintDOMUpdates <- true; { model with value = model.value + 1 }

let view (model : AdaptiveModel) =
    let things = List.init 20000 (fun i -> text (sprintf "%d" i)) |> IndexList.ofList
    let guhu = AList.ofIndexList things
    div [] [
        button [onClick (fun _ -> Inc)] [text "doIt"]

        Incremental.div AttributeMap.empty (
            List.init 100 id |> AList.ofList |> AList.mapA (fun _ ->
                model.value |> AVal.map (fun v ->
                    text (string v)
                )
            )
        )

        //Incremental.div AttributeMap.empty (
            
        //    model.value |> AList.bind (fun i -> 
        //        guhu
        //    )
        //)
    ]


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
