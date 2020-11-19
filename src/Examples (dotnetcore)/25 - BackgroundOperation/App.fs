module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open RenderControl.Model



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
        | UpdateInput v -> 
            model.currentInput.Put(v) |> ignore
            { model with progress = "starting" }
        | Result s -> { model with result = s; progress = "done" }
        | Progress f -> { model with progress = f }

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }


let view (model : AdaptiveModel) =

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


    div [onMouseMove (fun v -> UpdateInput v) ;style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
        div [style "grid-row: 1"] [
            text "Hello 3D"
            br []
            button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
            button [onClick (fun _ -> ToggleBackground)] [text "Change Background"]
        ]
        renderControl
        br []
        text "use first person shooter WASD + mouse controls to control the 3d scene"
        br []
        Incremental.text model.result
        br []
        br []
        Incremental.text model.progress
    ]

let threads (model : Model) = 
    let pool = FreeFlyController.threads model.cameraState |> ThreadPool.map Camera

    let rec backgroundOperation () = 
        proclist {
            do! Proc.SwitchToNewThread()
            let currentState = model.currentInput.Take()
            Log.startTimed "new input arrived"
            let mutable a = float currentState.X + 0.1
            let iters = 20
            for i in 0 .. iters do
                System.Threading.Thread.Sleep(10)
                a <- sin a * a
                yield (Progress (float i / float iters |> string))
            Log.stop ()
            yield Result (string a)
            yield! backgroundOperation()
        }

    ThreadPool.add "backgroundOperation" (backgroundOperation ()) pool


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
               background = C4b.Black
               currentInput = MVar.empty()
               result = ""
               progress = ""
            }
        update = update 
        view = view
    }
