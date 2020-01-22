namespace CorrelationDrawing

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
open UI.Composed
open Aardvark.Rendering.Text

open Aardvark.SceneGraph.SgPrimitives
open Aardvark.SceneGraph.FShadeSceneGraph
open CorrelationUtilities    

module Serialization =
    open MBrace.FsPickler
    open System.IO
    let binarySerializer = FsPickler.CreateBinarySerializer()
    
    let save (model : CorrelationAppModel) path = 
        let arr = binarySerializer.Pickle model
        File.WriteAllBytes(path, arr);

    let load path : CorrelationAppModel = 
        let arr = File.ReadAllBytes(path);
        let app = binarySerializer.UnPickle arr
        app

    let writeToFile path (contents : string) =
        System.IO.File.WriteAllText(path, contents)

module CorrelationDrawingApp = 
    open Newtonsoft.Json
            
    type Action =
        | CameraMessage    of ArcBallController.Message
        | DrawingMessage   of CorrelationDrawing.Action
        | SemanticMessage of CorrelationDrawing.Action
        | AnnotationMessage of CorrelationDrawing.Action
        | AddSemantic of CorrelationDrawing.Action
        | SetSemantic of CorrelationDrawing.Action
        | KeyDown of key : Keys
        | KeyUp of key : Keys      
        | Export
        | Save
        | Load
        | Clear
        | Undo
        | Redo
                       
    let stash (model : CorrelationAppModel) =
        { model with history = Some model; future = None }

    let clearUndoRedo (model : CorrelationAppModel) =
        { model with history = None; future = None }

    let update (model : CorrelationAppModel) (act : Action) =
        match act, model.drawing.draw with
            | CameraMessage m, false -> 
                    { model with camera = ArcBallController.update model.camera m }      
            | DrawingMessage m, _ ->
                    { model with drawing = CorrelationDrawing.update model.drawing m }    
            | AddSemantic m, false ->
                {model with drawing = CorrelationDrawing.update model.drawing m} 
            | SetSemantic m, false ->
                {model with drawing = CorrelationDrawing.update model.drawing m}      
            | SemanticMessage m, _ ->
                {model with drawing = CorrelationDrawing.update model.drawing m}          
            | AnnotationMessage m, _ ->
                {model with drawing = CorrelationDrawing.update model.drawing m}          
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
            // | EditSemantics, _ ->  
            | KeyDown k, _ -> 
                    let d = CorrelationDrawing.update model.drawing (CorrelationDrawing.Action.KeyDown k)
                    { model with drawing = d }
            | KeyUp k, _ -> 
                    let d = CorrelationDrawing.update model.drawing (CorrelationDrawing.Action.KeyUp k)
                    { model with drawing = d }
            | _ -> model
                       
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }
    
    let view (model : MCorrelationAppModel) =
                    
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
                            CorrelationDrawing.Sg.view model.drawing model.camera.view
                                |> Sg.map DrawingMessage
                                |> Sg.fillMode (model.rendering.fillMode)
                                    
                                |> Sg.cullMode (model.rendering.cullMode)                                                                    
                        )                                        
                ]
            
                div [style "width:35%; height: 100%; float:right;"] [
                    
                    div [clazz "ui buttons inverted"] [
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Save)] [
                                    i [clazz "save icon"] [] ] |> wrapToolTip "save"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Load)] [
                                    i [clazz "folder outline icon"] [] ] |> wrapToolTip "load"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Clear)] [
                                    i [clazz "file outline icon"] [] ] |> wrapToolTip "clear"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Export)] [
                                    i [clazz "external icon"] [] ] |> wrapToolTip "export"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Undo)] [
                                    i [clazz "arrow left icon"] [] ] |> wrapToolTip "undo"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Redo)] [
                                    i [clazz "arrow right icon"] [] ] |> wrapToolTip "redo"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Action.AddSemantic CorrelationDrawing.AddSemantic)] [
                                    i [clazz "plus icon"] [] ] //|> Utilities.wrapToolTip "add semantic"
                    ]

                    CorrelationDrawing.UI.viewAnnotationTools model.drawing |> UI.map DrawingMessage
                    CorrelationDrawing.UI.viewAnnotations model.drawing  |> UI.map AnnotationMessage   
                    CorrelationDrawing.UI.viewSemantics model.drawing |> UI.map SemanticMessage
                ]
            ]
        )

    let initial : CorrelationAppModel =
        {
            camera           = { ArcBallController.initial with view = CameraView.lookAt (23.0 * V3d.OIO) V3d.Zero V3d.OOI}
            rendering        = RenderingPars.initial
           
            drawing = CorrelationDrawing.insertFirstSemantics CorrelationDrawing.initial

           

            history = None
            future = None
        }

    let app : App<CorrelationAppModel,MCorrelationAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

