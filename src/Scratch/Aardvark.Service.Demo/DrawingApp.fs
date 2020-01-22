namespace UI.Composed

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.Rendering.Text

open PRo3DModels

open Aardvark.SceneGraph.SgPrimitives
open Aardvark.SceneGraph.FShadeSceneGraph

module Serialization =
    open MBrace.FsPickler
    open System.IO
    let binarySerializer = FsPickler.CreateBinarySerializer()
    
    let save (model : AnnotationAppModel) path = 
        let arr = binarySerializer.Pickle model
        File.WriteAllBytes(path, arr);

    let load path : AnnotationAppModel = 
        let arr = File.ReadAllBytes(path);
        let app = binarySerializer.UnPickle arr
        app

    let writeToFile path (contents : string) =
        System.IO.File.WriteAllText(path, contents)
       
module AnnotationApp = 
    open Newtonsoft.Json
            
    type Action =
        | CameraMessage    of ArcBallController.Message
        | DrawingMessage   of Drawing.Action
        | KeyDown of key : Keys
        | KeyUp of key : Keys      
        | Export
        | Save
        | Load
        | Clear
        | Undo
        | Redo
                       
    let stash (model : AnnotationAppModel) =
        { model with history = Some model; future = None }

    let clearUndoRedo (model : AnnotationAppModel) =
        { model with history = None; future = None }

    let update (model : AnnotationAppModel) (act : Action) =
        match act, model.drawing.draw with
            | CameraMessage m, false -> 
                    { model with camera = ArcBallController.update model.camera m }      
            | DrawingMessage m, _ ->
                    { model with drawing = Drawing.update model.drawing m }           
            | Save, _ -> 
                    Serialization.save model ".\drawing"
                    model
            | Load, _ -> 
                    Serialization.load ".\drawing"
            | Clear,_ ->
                    { model with drawing = { model.drawing with annotations = IndexList.empty }}            
            | Undo, _ -> 
                match model.history with
                    | Some h -> { h with future = Some model }
                    | None -> model
            | Redo, _ ->
                match model.future with
                    | Some f -> f
                    | None -> model
            | KeyDown k, _ -> 
                    let d = Drawing.update model.drawing (Drawing.Action.KeyDown k)
                    { model with drawing = d }
            | KeyUp k, _ -> 
                    let d = Drawing.update model.drawing (Drawing.Action.KeyUp k)
                    { model with drawing = d }
            | _ -> model
                       
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }
    
    let view (model : MAnnotationAppModel) =
                    
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        //let body att x =
        //    body [][ div att x ]

        require (Html.semui) (
            body [clazz "ui"; style "background: #1B1C1E"] [
                div [] [
                    ArcBallController.controlledControl model.camera CameraMessage frustum
                        (AttributeMap.ofList [
                                    onKeyDown (KeyDown)
                                    onKeyUp (KeyUp)
                                    attribute "style" "width:65%; height: 100%; float: left;"]
                        )
                        (
                            Drawing.Sg.view model.drawing model.camera.view
                                |> Sg.map DrawingMessage
                                |> Sg.fillMode model.rendering.fillMode
                                |> Sg.cullMode model.rendering.cullMode                                                                         
                        )                                        
                ]
            
                div [style "width:35%; height: 100%; float:right;"] [
                    
                    div [clazz "ui buttons inverted"] [
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Save)] [
                                    i [clazz "save icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Load)] [
                                    i [clazz "folder outline icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Clear)] [
                                    i [clazz "file outline icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Export)] [
                                    i [clazz "external icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Undo)] [
                                    i [clazz "arrow left icon"] [] ]
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Redo)] [
                                    i [clazz "arrow right icon"] [] ]
                    ]

                    Drawing.UI.viewAnnotationTools model.drawing |> UI.map DrawingMessage
                    Drawing.UI.viewAnnotations model.drawing               
                ]
            ]
        )
   
    let initialdrawing = {
        hoverPosition = None
        draw = false            

        working = None
        projection = Projection.Viewpoint
        geometry = Geometry.Polyline
        semantic = Semantic.Horizon3

        annotations = IndexList.empty

        exportPath = @"."
    }

    let initial : AnnotationAppModel =
        {
            camera           = { ArcBallController.initial with view = CameraView.lookAt (23.0 * V3d.OIO) V3d.Zero V3d.OOI}
            rendering        = InitValues.rendering
           
            drawing = initialdrawing

           

            history = None
            future = None
        }

    let app : App<AnnotationAppModel,MAnnotationAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

