module Golden.App

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Golden.Model

let initialTitle = "Golden Layout Example"

let initialCamera = {
    FreeFlyController.initial with
        view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
}

let layoutConfig =
    LayoutConfig.Default

let defaultLayout =
    layout {
        column {
            element {
                id "render"
                title "3D View"
                minSize 100
            }

            element {
                id "map"
                title "Map"
                header Header.Left
                buttons (Buttons.All ^^^ Buttons.Close)
            }
        }

        popout {
            element {
                id "aux2"
                title "Some pretty long title"
                buttons (Buttons.All ^^^ Buttons.Maximize)
                weight 1
            }

            width 300
            height 600
        }
    }

let private rnd = RandomSystem();

let update (model : Model) (msg : Message) =
    match msg with
    | Camera m ->
        { model with cameraState = FreeFlyController.update model.cameraState m }

    | CenterScene ->
        { model with cameraState = initialCamera }

    | LayoutChanged ->
        let title = $"{initialTitle} - {rnd.UniformInt(1000)}"
        { model with title = title }

    | GoldenLayout msg ->
        { model with golden = model.golden |> GoldenLayout.update msg }

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
        do! DefaultSurfaces.simpleLighting
    }

let view (model : AdaptiveModel) =
    pages (function
        | Pages.Page "render" ->
            let attributes =
                AttributeMap.ofListCond [
                    always <| style "width: 100%; height:100%; background: #2a2a2a"
                    always <| attribute "data-samples" "8"
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

        | Pages.Page "map" ->
            body [ style "width: 100%; height: 100%; border: 0; padding: 0; margin: 0; overflow: hidden" ] [
                iframe [
                    style "width: 100%; height: 100%"
                    attribute "src" "https://www.openstreetmap.org/export/embed.html?bbox=-0.004017949104309083%2C51.47612752641776%2C0.00030577182769775396%2C51.478569861898606&layer=mapnik"
                ] []
            ]

        | Pages.Page "aux2" ->
            div [style "color: white; padding: 10px"] [
                button [onClick (fun _ -> Message.GoldenLayout GoldenLayout.Message.ResetLayout)] [
                    text "Reset layout"
                ]

                button [onClick (fun _ -> Message.GoldenLayout (GoldenLayout.Message.SaveLayout "GoldenLayoutExample.Key"))] [
                    text "Save layout"
                ]

                button [onClick (fun _ -> Message.GoldenLayout (GoldenLayout.Message.LoadLayout "GoldenLayoutExample.Key"))] [
                    text "Load layout"
                ]
            ]

        | Pages.Page unknown ->
            div [style "color: red; padding: 10px"] [
                text $"Unknown element: {unknown}"
            ]

        | Pages.Body ->
            Html.title false model.title (
                body [style "width: 100%; height: 100%; overflow: hidden; margin: 0"] [
                    let attributes = [
                        style "width: 100%; height: 100%; min-width: 400px; min-height: 400px; overflow: hidden"
                        onLayoutChanged' (fun _ -> LayoutChanged)
                    ]

                    GoldenLayout.view attributes model.golden
                ]
            )
    )

let threads (model : Model) =
    ThreadPool.empty

let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
                cameraState = initialCamera
                title = initialTitle
                golden = GoldenLayout.create layoutConfig defaultLayout
            }
        update = update
        view = view
    }
