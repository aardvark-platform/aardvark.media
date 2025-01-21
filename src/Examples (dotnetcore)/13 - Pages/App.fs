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
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }

        | CenterScene -> 
            { model with cameraState = initialCamera }

        | UpdateConfig cfg ->
            { model with dockConfig = cfg; past = Some model }

        | SetCullMode mode ->
            { model with cullMode = mode; past = Some model }

        | ToggleFill ->
            { model with fill = not model.fill; past = Some model }

        | SetFiles files -> 
            { model with files = IndexList.ofList files }

        | Undo ->
            match model.past with
                | Some p -> { p with future = Some model; cameraState = model.cameraState }
                | None -> model

        | Redo ->
            match model.future with
                | Some f -> { f with past = Some model; cameraState = model.cameraState }
                | None -> model

          
let deps = [
    { name = "failing"; kind = Stylesheet; url = "notfound"}
    { name = "failing2"; kind = Script; url = "notfound"}
]

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
    }
    |> Sg.cullMode model.cullMode
    |> Sg.fillMode (model.fill |> AVal.map (function true -> FillMode.Fill | false -> FillMode.Line))
let view (model : AdaptiveModel) =


    let toggleBox (str : string) (state : aval<bool>) (toggle : 'msg) =

        let attributes = 
            amap {
                    yield attribute "type" "checkbox"
                    yield onChange (fun _ -> toggle)
                    let! check = state
                    if check then
                        yield attribute "checked" "checked"
            }

        onBoot "$('#__ID__').checkbox()" (
            div [clazz "ui small toggle checkbox"] [
                Incremental.input (AttributeMap.ofAMap attributes)
                label [] [text str]
            ]
        )


    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    (AttributeMap.ofList [ style "width: 100%; height:100%"; attribute "data-samples" "8" ]) 
                    (viewScene model)

    page (fun request -> 
        match Map.tryFind "page" request.queryParams with
            | Some "controls" -> 
                require Html.semui (
                    body [  style "width: 100%; height:100%; background: transparent; overflow: auto"; ] [
                        table [clazz "ui very compact unstackable striped inverted table"; style "border-radius: 0;"] [
                            tr [] [
                                td [] [text "CullMode"]
                                td [] [Html.SemUi.dropDown model.cullMode SetCullMode]
                            ]
                            tr [] [
                                td [] [text "Fill"]
                                td [] [toggleBox "" model.fill ToggleFill]
                            ]
                        ]
                        br []
                        br []
                        button [
                            onChooseFiles SetFiles
                            clientEvent "onclick" ("top.aardvark.dialog.showOpenDialog({properties: ['openFile', 'openDirectory', 'multiSelections']}).then(result => aardvark.processEvent('__ID__', 'onchoosefile', result.filePaths));"
                            ) 
                        ] [text "open directory"]
                        button [style "position: absolute; bottom: 5px; left: 5px;"; clazz "ui small button"; onClick (fun _ -> CenterScene)] [text "Center Scene"]
                    ]
                )

            | Some "render" -> 
                body [] [
                    renderControl
                ]

            | Some "meta" ->
                require deps (
                    body [] [
                        button [onClick (fun _ -> Undo)] [text "Undo"]
                        br []
                        Incremental.div AttributeMap.empty <| AList.map text model.files
                    ]
                )

            | Some other ->
                let msg = sprintf "Unknown page: %A" other
                body [] [
                    div [style "color: white; font-size: large; background-color: red; width: 100%; height: 100%"] [text msg]
                ]  

            | None -> 
                model.dockConfig |> AVal.force |> AVal.constant |> docking [
                    style "width:100%;height:100%;"
                    onLayoutChanged UpdateConfig
                ]
    )

let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map Camera

let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
                past = None
                future = None
                cameraState = initialCamera
                cullMode = CullMode.None
                fill = true
                files = IndexList.empty
                dockConfig =
                    config {
                        content (
                            horizontal 10.0 [
                                element { id "render"; title "Render View"; weight 20; isCloseable true }
                                vertical 5.0 [
                                    element { id "controls"; title "Controls"; weight 5 }
                                    element { id "meta"; title "Layout Controls"; weight 5; isCloseable false }
                                ]
                            ]
                        )
                        appName "PagesExample"
                        useCachedConfig false
                    }
            }
        update = update 
        view = view
    }
