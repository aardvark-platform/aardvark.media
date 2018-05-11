module SimpleScaleApp

open SimpleScaleModel
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Base.Incremental
open UI.Composed
open PRo3DModels
open Aardvark.Base
open Aardvark.Base.Rendering

type Action =
        | CameraMessage     of ArcBallController.Message
        | RenderingAction   of RenderingProperties.Action                
        | ChangeScale       of Vector3d.Action

let update (model : Model) (act : Action) : Model =
        match act with
        | CameraMessage a   -> { model with camera = ArcBallController.update model.camera a}
        | RenderingAction a -> { model with rendering = RenderingProperties.update model.rendering a }
        | ChangeScale a     -> { model with scale = Vector3d.update model.scale a }

let view (model : MModel) =
    
    let frustum = Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
            
    require (Html.semui) (
        div [clazz "ui"; style "background-color: #1B1C1E"] [
            ArcBallController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                    attribute "style" "width:65%; height: 100%; float: left;"
                ])
                (
                    let boxGeometry = Box3d(-V3d.III, V3d.III)
                    let box = Mod.constant (boxGeometry)   
                        
                    let localScaleTrafo = model.scale.value |> Mod.map(fun d -> Trafo3d.Scale d)

                    let b = Sg.box (Mod.constant C4b.Blue) box

                    let s = Sg.sphere 5 (Mod.constant C4b.Red) (Mod.constant 2.0)
                                |> Sg.trafo localScaleTrafo

                    [b; s]  |> Sg.ofList
                            |> Sg.shader {
                                    do! DefaultSurfaces.trafo
                                    do! DefaultSurfaces.vertexColor
                                    do! DefaultSurfaces.simpleLighting
                                }
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode  
                            |> Sg.noEvents
                )

            div [style "width:35%; height: 100%; float:right; background-color: #1B1C1E"] [
                Html.SemUi.accordion "Rendering" "configure" true [
                    RenderingProperties.view model.rendering |> UI.map RenderingAction 
                ]

                Html.SemUi.accordion "Scale" "configure" true [                      
                    yield Vector3d.view model.scale |> UI.map ChangeScale
                ]
            ]
        ]
    )

let initial =
    {
        camera = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
        rendering = { InitValues.rendering with cullMode = CullMode.None }    
        scale = Vector3d.init
    }

let app : App<Model, MModel, Action> =
    {
        unpersist   = Unpersist.instance
        threads     = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
        initial     = initial
        update      = update
        view        = view
    }

let start () = App.start app