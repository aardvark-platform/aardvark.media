namespace UI.Composed

open System
open Aardvark.UI
open Aardvark.Base.Incremental
open Aardvark.SceneGraph.AirState
open Demo

open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text
open Demo.TestApp
open Demo.TestApp.Mutable

module SimpleCompositionViewer = 
    open PRo3DModels
    open Aardvark.Base
    
    type Action =
        | CameraMessage    of CameraController.Message
        | AnnotationAction of AnnotationProperties.Action
        | RenderingAction  of RenderingProperties.Action
        | MyOwnAction

    let update (model : ComposedViewerModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }
            | AnnotationAction a ->
                 { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | MyOwnAction -> model

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

        require Html.semui (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:60%; height: 100%; float: left;"
                    ])
                    (
                        let box' = Mod.constant (Box3d(-V3d.III, V3d.III))
                        let color = Mod.constant(C4b.Red)
                        let box =
                            Sg.box color box'
                                |> Sg.shader {
                                    do! DefaultSurfaces.trafo
                                    do! DefaultSurfaces.vertexColor
                                    do! DefaultSurfaces.simpleLighting
                                    }
                                |> Sg.noEvents                            
                                |> Sg.fillMode model.rendering.fillMode
                                |> Sg.cullMode model.rendering.cullMode

                        box |> Sg.noEvents                        
                )

                div [style "width:40%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]
                    Html.SemUi.accordion "Properties" "options" true [
                        AnnotationProperties.view model.singleAnnotation |> UI.map AnnotationAction
                    ]
                ]
        ])

    let initial =
        {
            camera           = CameraController.initial
            singleAnnotation = InitValues.annotation
            rendering        = InitValues.rendering
        }

    let app =
        {
            unpersist = Unpersist.instance
            threads = fun model -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app