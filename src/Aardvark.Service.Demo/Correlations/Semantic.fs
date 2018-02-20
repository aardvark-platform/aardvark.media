namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.UI


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Semantic = 
    let initial = {
        label = "Semantic1" 
        elevation = 0.0
        azimuth = 0.0
        size = 0.0
        style = {Style.color = C4b.Red; Style.thickness = {Numeric.init with value = 1.0}}
        geometry = GeometryType.Line
    }

    type Action = ChangeLabel | ChangeColor | ChangeThickness

    let update (sem : Semantic) (a : Action) = 
        match a with
            | ChangeLabel -> {sem with label = "OtherLabel"}
            | ChangeColor -> {sem with style = {sem.style with color = C4b.Blue}}
            | ChangeThickness -> {sem with style = {sem.style with thickness = {Numeric.init with value =  2.0}}}

    let view (s : MSemantic) =
        //require Html.semui (             
            div [clazz "ui buttons"][
                button [clazz "ui button"; onMouseClick (fun _ -> ChangeLabel)] [text (Mod.force s.label)]
                button [clazz "ui button"; onMouseClick (fun _ -> ChangeColor)] [text "Color"]
                button [clazz "ui button"; onMouseClick (fun _ -> ChangeThickness)] [text "Thickness"]
            ]
       // )