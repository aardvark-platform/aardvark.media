module Inc.App


open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering

open Aardvark.UI
open Aardvark.UI.Primitives

open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        | Inc -> { model with value = model.value + 1 }
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }

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


let view (sg : ISg<_>) (model : MModel) =
    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    (AttributeMap.ofList [ style "width: 400px; height:400px; background: #222"; attribute "data-samples" "8"]) 
                    sg

    require Html.semui (
        //body [style "background: rgb(27, 28, 29);"] [
        //    menu ()
        //]
        onBoot "aardvark.processEvent('__ID__', 'createClient');" (
            onShutdown "" (
                body [] [
                    renderControl
                ]
            )
        )
    )


let threads (model : Model) = 
    ThreadPool.empty

let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let app sg =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               value = 0
               cameraState = initialCamera
            }
        update = update 
        view = view sg
    }
