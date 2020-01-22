namespace CorrelationDrawing

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.UI


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Semantic = 
    let initial = {
        label = "Semantic1" 
        size = 0.0
        style = {Style.color = {c = C4b.Red}; Style.thickness = {Numeric.init with value = 1.0}}
        geometry = GeometryType.Line
        semanticType = SemanticType.Metric
    }

    type Action = 
        | ChangeLabel
        | ColorPickerMessage of ColorPicker.Action
        | ChangeThickness

    let update (sem : Semantic) (a : Action) = 
        match a with
            | ChangeLabel -> {sem with label = "OtherLabel"}
            | ColorPickerMessage m -> 
                {sem with style = {sem.style with color = (ColorPicker.update sem.style.color m)}}

            | ChangeThickness -> {sem with style = {sem.style with thickness = {Numeric.init with value =  2.0}}}

    let view (s : MSemantic) =
        //require Html.semui (             
        div [clazz "ui"][
            button [clazz "ui button"; onMouseClick (fun _ -> ChangeLabel)] [text (AVal.force s.label)]
            ColorPicker.view s.style.color |> UI.map ColorPickerMessage
            button [clazz "ui button"; onMouseClick (fun _ -> ChangeThickness)] [text "Thickness"]
        ]
       // )