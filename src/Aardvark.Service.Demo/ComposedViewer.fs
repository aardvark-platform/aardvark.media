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
        | Enter of string
        | Exit      

    let update (model : ComposedViewerModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }
            | AnnotationAction a ->
                 { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | Enter id-> { model with boxHovered = Some id }
            | Exit -> { model with boxHovered = None }

    //let createBox (color: IMod<C4b>) (box : Box3d) =
    //    let b = Mod.constant box
    //    Sg.box color b
    //            |> Sg.shader {
    //                do! DefaultSurfaces.trafo
    //                do! DefaultSurfaces.vertexColor
    //                do! DefaultSurfaces.simpleLighting
    //                }
    //            |> Sg.noEvents
    //            //|> Sg.pickable (PickShape.Box boxM)
    //            //|> Sg.withEvents [
    //            //    Sg.onEnter (fun p -> Enter)
    //            //    Sg.onLeave (fun () -> Exit)]
        
    let hoveredColor (model : MComposedViewerModel) (box : VisibleBox) =
        model.boxHovered |> Mod.map (fun h -> match h with
                                                | Some i -> if i = box.id then C4b.Green else box.color
                                                | None -> box.color)
    
    let mkVisibleBox (color : C4b) (box : Box3d)= 
        {
            id = Guid.NewGuid().ToString()
            geometry = box
            color = color
        }

    let mkISgBox (model : MComposedViewerModel) (box : VisibleBox) =
        let box' = Mod.constant (box.geometry)
        let c = hoveredColor model box

        Sg.box c box'
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                    }
                |> Sg.noEvents
                |> Sg.pickable (PickShape.Box box.geometry)
                |> Sg.withEvents [
                        Sg.onEnter (fun p -> Enter box.id)
                        Sg.onLeave (fun () -> Exit)]                    

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

        require Html.semui (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        let colors = [C4b.Red; C4b.Blue]
                        let boxes = [Box3d(-V3d.III, V3d.III); Box3d(-V3d.III + V3d.IOO * 2.5, V3d.III + V3d.IOO * 2.5)]
                                              
                        let boxes = boxes 
                                    |> List.mapi (fun i k -> mkVisibleBox colors.[i] k)
                                    |> List.map (fun k -> mkISgBox model k)                                                    
                        boxes
                            |> Sg.ofList
                            |> Sg.noEvents     
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                            |> Sg.noEvents                        
                )

                div [style "width:35%; height: 100%; float:right"] [
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

            boxes = []
            boxHovered = None
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