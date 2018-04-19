module RenderingParameters

open Aardvark.Base.Rendering
open Aardvark.UI

open RenderingParametersModel


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