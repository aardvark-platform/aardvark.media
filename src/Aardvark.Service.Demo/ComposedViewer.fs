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

module ComposedViewer = 
    open PRo3DModels
    open Aardvark.Base
    
    type Action =
        | CameraMessage of CameraController.Message
        | AnnotationAction of AnnotationProperties.Action
        | MyOwnAction

    let update (model : ComposedViewerModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }
            | AnnotationAction a ->
                 { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | MyOwnAction -> model

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

        CameraController.controlledControl model.camera CameraMessage frustum
            (AttributeMap.ofList [
                attribute "style" "width:100%; height: 100%"
                //onRendered (fun _ _ _ -> TimeElapsed)
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
                        //|> Sg.pickable (PickShape.Box baseBox)        
                                 

                let sg = 
                    box |> Sg.noEvents                        
                sg
            )

    let initial =
        {
            camera = CameraController.initial
            singleAnnotation = InitValues.initAnnotation
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