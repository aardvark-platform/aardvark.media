namespace screenhotr.example

open FSharp.Data.Adaptive

open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Screenshotr

open screenhotr.example.Model

module App =
    
    let initial (url : string) = 
        { 
            cameraState = FreeFlyController.initial
            screenshotr = ScreenshotrModel.Default url // Step 3: initialize the ScreenshotrModel with your application url
        }

    let update (m : Model) (msg : Message) =
        match msg with
            | CameraMessage msg -> { m with cameraState = FreeFlyController.update m.cameraState msg }
            
            // Step 4: add the ScreenshotrMessage to your update function
            | ScreenshotrMessage msg -> { m with screenshotr = ScreenshotrUpdate.update msg m.screenshotr }
            
            | Message.KeyDown k -> 
                match k with
                // Step 5: add some key (or button) bindings in your update function
                | Keys.F8 -> { m with screenshotr = m.screenshotr |> ScreenshotrUpdate.update ToggleScreenshotUi }
                | _ -> m


    let view (m : AdaptiveModel) =

        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant

        let scene =
            [| 
                Sg.sphere' 8 C4b.ForestGreen 1.0
                Sg.box' C4b.CornflowerBlue Box3d.Unit |> Sg.translate 2.0 1.0 0.0
                Sg.cone' 125 C4b.DarkYellow 1.0 2.0 |> Sg.translate (-2.0) -3.0 0.0
                Sg.cylinder' 30 C4b.Tomato 1.0 2.0 |> Sg.translate 2.0 -3.0 0.0
            |]
            |> Sg.ofArray
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

        let att =
            [
                style "position: fixed; left: 0; top: 0; width: 100%; height: 100%"
                onKeyDown (KeyDown)
            ]

        let dependencies =  [] @ Html.semui

        body [] [
            FreeFlyController.controlledControl m.cameraState CameraMessage frustum (AttributeMap.ofList att) scene

            // Step 6: add the screenshotr UI to your UI
            ScreenshotrView.screenshotrUI m.screenshotr |> UI.map ScreenshotrMessage
        ]
        |>  require dependencies

    let app (myUrl : string) =
        {
            initial = initial myUrl
            update = update
            view = view
            threads = fun m -> m.cameraState |> FreeFlyController.threads |> ThreadPool.map CameraMessage
            unpersist = Unpersist.instance
        }
