module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Inc.Model

let initialCamera = {
    FreeFlyController.initial with
        view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
}

let update (model : Model) (msg : Message) =
    match msg with
    | ToggleBackground ->
        { model with background = (if model.background = C4b.Black then C4b.White else C4b.Black) }
    | Camera m ->
        { model with cameraState = FreeFlyController.update model.cameraState m }
    | CenterScene ->
        { model with cameraState = initialCamera }

let dependencies = [
    { name = "js"; url = "http://code.jquery.com/jquery-1.11.1.min.js"; kind = Script }
    { name = "Golden"; url = "https://golden-layout.com/files/latest/js/goldenlayout.min.js"; kind = Script }
    { name = "GoldenCSS"; url = "https://golden-layout.com/files/latest/css/goldenlayout-base.css"; kind = Stylesheet }
    { name = "Theme"; url = "https://golden-layout.com/files/latest/css/goldenlayout-dark-theme.css"; kind = Stylesheet }
    { name = "GoldenAard"; url = "resources/goldenAard.js"; kind = Script }
]

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
    }


let view3d (model : AdaptiveModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant)
                    (AttributeMap.ofListCond [
                        always <| style "width: 100%; grid-row: 2; height:100%";
                        always <| attribute "showFPS" "true";         // optional, default is false
                        "style", model.background |> AVal.map(fun c -> sprintf "background: #%02X%02X%02X" c.R c.G c.B |> AttributeValue.String |> Some)
                        //attribute "showLoader" "false"    // optional, default is true
                        //attribute "data-renderalways" "1" // optional, default is incremental rendering
                        always <| attribute "data-samples" "8"        // optional, default is 1
                    ])
            (viewScene model)


    div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
        div [style "grid-row: 1"] [
            text "Hello 3D"
            br []
            button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
            button [onClick (fun _ -> ToggleBackground)] [text "Change Background"]
        ]
        renderControl
        br []
        text "use first person shooter WASD + mouse controls to control the 3d scene"
    ]

let view (model : AdaptiveModel) =


    page (fun request -> 
        match Map.tryFind "page" request.queryParams with
            | Some "render" -> 
                view3d model
            | _ -> 
                require dependencies (
                    onBoot "aardvark.initLayout()" (
                        body [] []
                    )
                )
    )


let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {   
                cameraState = initialCamera
                background = C4b.Black
            }
        update = update 
        view = view
    }
