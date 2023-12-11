module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

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

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
    }

let view (model : AdaptiveModel) =

    let layout =
        row {
            column {
                element {
                    id "render"
                    title "3D View"
                }

                element {
                    id "map"
                    title "Map"
                    header Header.Top
                    buttons (Buttons.All ^^^ Buttons.Close)
                }

                weight 2
            }

            element {
                id "aux2"
                title "Some pretty long title"
                buttons (Buttons.All ^^^ Buttons.Maximize)
                weight 1
            }
        }

    body [style "width: 100%; height: 100%; overflow: hidden; margin: 0"] [
        let attributes = [
            style "width: 100%; height: 100%; min-width: 400px; min-height: 400px; overflow: hidden"
        ]

        GoldenLayout.layout attributes LayoutConfig.Default layout (fun element ->
            match element with
            | "render" ->
                let attributes =
                    AttributeMap.ofListCond [
                        always <| style "width: 100%; height:100%"
                        always <| attribute "data-samples" "8"
                        "style", model.background |> AVal.map(fun c -> sprintf "background: #%02X%02X%02X" c.R c.G c.B |> AttributeValue.String |> Some)
                    ]

                let frustum =
                    Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant

                let sg =
                    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.vertexColor
                        do! DefaultSurfaces.simpleLighting
                    }

                FreeFlyController.controlledControl model.cameraState Camera frustum attributes sg

            | "map" ->
                iframe [
                    style "width: 100%; height: 100%"
                    attribute "src" "https://www.openstreetmap.org/export/embed.html?bbox=-0.004017949104309083%2C51.47612752641776%2C0.00030577182769775396%2C51.478569861898606&layer=mapnik"
                ] []

            | "aux2" ->
                div [style "color: white; padding: 10px"] [
                    text "What's up?"
                ]

            | _ ->
                div [style "color: red; padding: 10px"] [
                    text $"Unknown element: {element}"
                ]
        )
    ]

let threads (model : Model) =
    ThreadPool.empty

let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
                cameraState = initialCamera
                background = C4b(34,34,34)
            }
        update = update
        view = view
    }
