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

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let view (model : MModel) =

    let renderControl =
        CameraController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 400px; height:400px"]) 
                    (viewScene model)

    body [] [
        text "Hello 3D"
        br []
        button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
        br []
        renderControl
    ]

// variant with html5 grid layouting (currently not working in our cef)
let view2 (model : MModel) =

    let renderControl =
        CameraController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 100%; grid-row: 2"]) 
                    (viewScene model)

    body [] [
        div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
            div [style "grid-row: 1"] [
                text "Hello 3D"
                br []
                button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
            ]
            renderControl
            br []
            text "use first person shooter WASD + mouse controls to control the 3d scene"
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
