module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let initialCamera = { 
        CameraController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = CameraController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | LoadFiles s -> 
            printfn "%A" s
            model

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let onChooseFiles (chosen : list<string> -> 'msg) =
    onEvent "onchoose" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> List.map Aardvark.Service.PathUtils.ofUnixStyle >> chosen)
        

// variant with html5 grid layouting (currently not working in our cef)
let view (model : MModel) =

    let renderControl =
        CameraController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 100%; grid-row: 2"]) 
                    (viewScene model)

    body [] [
        div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
            button [
                onChooseFiles (fun files -> printfn "%A" files; LoadFiles files)
                clientEvent "onclick" ("aardvark.processEvent('__ID__', 'onchoose', aardvark.dialog.showOpenDialog({properties: ['openFile', 'openDirectory', 'multiSelections']}));") 
            ] [text "open directory"]

        ]
    ]

let threads (model : Model) = 
    CameraController.threads model.cameraState |> ThreadPool.map Camera


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
            }
        update = update 
        view = view
    }
