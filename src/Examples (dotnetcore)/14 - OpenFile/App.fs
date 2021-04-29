module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model


let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | OpenFiles m -> 
            { model with currentFiles = IndexList.ofList m }

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }



let view (model : AdaptiveModel) =
    require Html.semui (
        body [ style "background: black"] [
            div [clazz "ui inverted segment" ] [
                openDialogButton 
                    { OpenDialogConfig.file with allowMultiple = true; title = "ROCK THE POWER. ROCKET POWER" }
                    [ clazz "ui green button"; onChooseFiles OpenFiles ] 
                    [ text "Open File" ]
            ]


            div [clazz "ui inverted segment"] [
                Incremental.div (AttributeMap.ofList [clazz "ui inverted relaxed divided list"]) (
                    model.currentFiles |> AList.map (fun f ->
                        div [clazz "item"] [
                            div [ clazz "content" ] [
                                div [clazz "ui orange label"] [ text f ] 
                            ]
                        ]
                    )
                )
            ]
        ]
    )

let threads (model : Model) = 
    ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               currentFiles = IndexList.empty
            }
        update = update 
        view = view
    }
