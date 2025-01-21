module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open RenderControl.Model


let update (model : Model) (msg : Message) =
    match msg with
        | Log s -> 
            { model with info = s }
        | Done(id,r) -> 
            { model with result = r; threads = ThreadPool.remove id model.threads }
        | Start -> 
            let id = System.Guid.NewGuid().ToString()
            let worker =    
                proclist {
                    yield Log "Begin"
                    do! Proc.Sleep 100
                    for i in 0 .. 100 do
                        do! Proc.Sleep 10
                        yield Log (sprintf "got: %d%%" i)
                    yield Done(id,System.Random().NextDouble())
                }
            { model with threads = ThreadPool.start worker model.threads }
        

let view (model : AdaptiveModel) =
    div [] [
        button [onClick (fun _ -> Start)] [text "start"]
        Incremental.text (AVal.map string model.result)
        br []
        Incremental.text model.info
    ]


let threads (model : Model) = 
    model.threads


let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
                threads = ThreadPool.empty
                info = ""
                result = 1.0
            }
        update = update 
        view = view
    }
