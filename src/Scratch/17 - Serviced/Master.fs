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
        | Nop -> model


let viewScene (model : MMasterModel) = 
    Sg.box (Mod.constant C4b.White) (Mod.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
       }
       
let view (model : MMasterModel) =
    let scene = viewScene model

    let mapOut (m : Model) (msg : Message) = 
        Seq.empty 

    let mapIn (m : Model) (msg : MasterMessage) = 
        Seq.empty

    div [] [
        br []
        onBoot "console.log('boot')" (
            onShutdown "console.log('shtudown')" (
                subApp' mapOut mapIn [] (Inc.App.app scene)
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
