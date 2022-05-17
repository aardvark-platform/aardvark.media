namespace screenhotr.example

open System
open FSharp.Data.Adaptive

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

open screenhotr.example.Model

type Message =
    | CameraMessage of FreeFlyController.Message
    | TakeScreenshot

module App =
    
    let initial = { cameraState = FreeFlyController.initial }

    let update (m : Model) (msg : Message) =
        match msg with
            | CameraMessage msg ->
                { m with cameraState = FreeFlyController.update m.cameraState msg }
            | TakeScreenshot -> m

    let view (m : AdaptiveModel) =

        let frustum = 
            Frustum.perspective 60.0 0.1 100.0 1.0 
                |> AVal.constant

        let sg =
            Sg.empty
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

        let att =
            [
                style "position: fixed; left: 0; top: 0; width: 100%; height: 100%"
            ]

        body [] [
            FreeFlyController.controlledControl m.cameraState CameraMessage frustum (AttributeMap.ofList att) sg

            div [style "position: fixed; left: 20px; top: 20px"] [
                button [onClick (fun _ -> TakeScreenshot)] [text "Take Screenshot"]
            ]

        ]

    let app =
        {
            initial = initial
            update = update
            view = view
            threads = fun m -> m.cameraState |> FreeFlyController.threads |> ThreadPool.map CameraMessage
            unpersist = Unpersist.instance
        }
