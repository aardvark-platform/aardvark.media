namespace UI.Composed

open System
open Aardvark.UI
open Aardvark.Base.Incremental
open Aardvark.SceneGraph.AirState
open PRo3DModels    

module NavigationProperties = 
        
    type Action =
        | SetNavigationMode of NavigationMode

    let update (model : NavigationParameters) (act : Action) =
        match act with
            | SetNavigationMode mode ->
                    { model with navigationMode = mode }

    let view (model : MNavigationParameters) =        
        require Html.semui (
            Html.table [                            
               Html.row "Mode:" [Html.SemUi.dropDown model.navigationMode SetNavigationMode]
            ]
        )


module RenderingProperties = 
    open Aardvark.Base.Rendering

    type Action =
        | SetFillMode of FillMode
        | SetCullMode of CullMode

    let update (model : RenderingParameters) (act : Action) =
        match act with
            | SetFillMode mode ->
                    { model with fillMode = mode }
            | SetCullMode mode ->
                    { model with cullMode = mode }

    let view (model : MRenderingParameters) =        
        require Html.semui (
            Html.table [                            
                            Html.row "FillMode:" [Html.SemUi.dropDown model.fillMode SetFillMode]
                            Html.row "CullMode:" [Html.SemUi.dropDown model.cullMode SetCullMode]      
                       ]
        )

module AnnotationProperties = 
    
    open Aardvark.Base
    
    type Action = 
        | SetGeometry     of Geometry
        | SetProjection   of Projection
        | ChangeThickness of Numeric.Action
        | SetText         of string
        | ToggleVisible

    let update (model : Annotation) (act : Action) =
        match act with
            | SetGeometry mode ->
                { model with geometry = mode}
            | SetProjection mode ->
                { model with projection = mode}
            | ChangeThickness a ->
                { model with thickness = Numeric.update model.thickness a}
            | SetText t ->
                { model with text = t}
            | ToggleVisible ->
                { model with visible = (not model.visible)}

    let view (model : MAnnotation) =
        

        require Html.semui (
            Html.table [                            
                            Html.row "Geometry:" [Html.SemUi.dropDown model.geometry SetGeometry]
                            Html.row "Projection:" [Html.SemUi.dropDown model.projection SetProjection]      
                            Html.row "Thickness:" [div [clazz "ui input"] [ Numeric.view' [InputBox] model.thickness |> UI.map ChangeThickness ]]
                            Html.row "Text:" [Html.SemUi.textBox model.text SetText ]
                            Html.row "Visible:" [Html.SemUi.toggleBox model.visible ToggleVisible ]
                        ]
        )

    let app = 
        {
            unpersist = Unpersist.instance
            threads = fun _ -> ThreadPool.empty
            initial = InitValues.annotation
            update = update
            view = view
        }

    let start() = App.start app