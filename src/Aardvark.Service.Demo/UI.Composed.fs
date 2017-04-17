namespace UI.Composed

open System
open Aardvark.UI
open Aardvark.Base.Incremental
open Aardvark.SceneGraph.AirState

module AnnotationProperties = 
    open PRo3DModels    
    open Aardvark.Base
    
    type Action = 
        | SetGeometry     of Geometry
        | SetProjection   of Projection
        | ChangeThickness of Numeric.Action
        | SetText         of string

    let update (model : Annotation) (msg : Action) =
        match msg with
            | SetGeometry mode ->
                { model with geometry = mode}
            | SetProjection mode ->
                { model with projection = mode}
            | ChangeThickness a ->
                { model with thickness = Numeric.update model.thickness a}
            | SetText t ->
                { model with text = t}

    let view (model : MAnnotation) =

        require Html.semui (
            Html.table [                            
                            Html.row "Geometry:" [Html.SemUi.dropDown model.geometry SetGeometry]
                            Html.row "Projection:" [Html.SemUi.dropDown model.projection SetProjection]      
                            Html.row "Thickness:" [div [clazz "ui input"] [ Numeric.view' [InputBox] model.thickness |> UI.map ChangeThickness ]]
                            Html.row "Text:" [Html.SemUi.textBox model.text SetText ]
                        ]
        )

    let app = 
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.create()
            initial = InitValues.initAnnotation
            update = update
            view = view
        }

    let start() = App.start app