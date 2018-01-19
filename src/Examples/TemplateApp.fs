module Examples.TemplateApp

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open Examples.TemplateModel

let update (m : Model) (message : Message) =
    match message with
        | CameraMessage msg -> { m with camera = CameraController.update m.camera msg }

let viewScene (m : MModel) =
    Sg.empty

let view (m : MModel) =
    body [ style "background: #1B1C1E"] [
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl m.camera CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ attribute "style" "width:85%; height: 100%; float: left;"]) (viewScene m)

                div [style "width:15%; height: 100%; float:right"] [
                    Html.SemUi.stuffStack [
                        button [clazz "ui button"; ] [text "Hello World"]
                        br []

                    ]
                ]
            ]
        )
    ]

let threads (m : Model) =
    ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
                camera = CameraController.initial 
            }
        update = update 
        view = view
    }
