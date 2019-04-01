module Inc.Master

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model

let update (model : MasterModel) (msg : MasterMessage) =
    match msg with
        | ResetAll -> model

let view (model : MMasterModel) =
    div [] [
        br []
        onBoot "console.log('boot')" (
            onShutdown "console.log('shtudown')" (
                subApp Inc.App.app
            )
        )
    ]


let threads (model : MasterModel) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               clients = HMap.empty
            }
        update = update 
        view = view
    }
