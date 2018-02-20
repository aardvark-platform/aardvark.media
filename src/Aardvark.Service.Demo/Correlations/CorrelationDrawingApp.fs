namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open UI.Composed
open Aardvark.Rendering.Text

open Aardvark.SceneGraph.SgPrimitives
open Aardvark.SceneGraph.FShadeSceneGraph
       

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
        | SemanticMessage of Semantic.Action
        | AddSemantic of CorrelationDrawing.Action
//        | EditSemantic of CorrelationDrawing.Action
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
//            | EditSemantic cs, _ ->
//                    { model with drawing = CorrelationDrawing.update model.drawing cs}
            | Save, _ -> 
                    Serialization.save model ".\drawing"
                    model
            | Load, _ -> 
                    Serialization.load ".\drawing"
            | Clear,_ ->
                    { model with drawing = { model.drawing with annotations = PList.empty }}            
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
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
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
                                    i [clazz "save icon"] [] ] |> Utilities.wrapToolTip "save"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Load)] [
                                    i [clazz "folder outline icon"] [] ] |> Utilities.wrapToolTip "load"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Clear)] [
                                    i [clazz "file outline icon"] [] ] |> Utilities.wrapToolTip "clear"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Export)] [
                                    i [clazz "external icon"] [] ] |> Utilities.wrapToolTip "export"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Undo)] [
                                    i [clazz "arrow left icon"] [] ] |> Utilities.wrapToolTip "undo"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Redo)] [
                                    i [clazz "arrow right icon"] [] ] |> Utilities.wrapToolTip "redo"
                        button [clazz "ui icon button"; onMouseClick (fun _ -> Action.AddSemantic CorrelationDrawing.AddSemantic)] [
                                    i [clazz "plus icon"] [] ] //|> Utilities.wrapToolTip "add semantic"
                    ]

                    CorrelationDrawing.UI.viewAnnotationTools model.drawing |> UI.map DrawingMessage
                    CorrelationDrawing.UI.viewAnnotations model.drawing    
                    CorrelationDrawing.UI.viewSemantics model.drawing
                ]
            ]
        )
   
    //let initialdrawing = {
        //hoverPosition = None
        //draw = false            

        //working = None
        //projection = Projection.Viewpoint
        //geometry = GeometryType.Polyline
        //semantic = Semantic.initial

        //annotations = PList.empty

        //exportPath = @"."
    //}

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

