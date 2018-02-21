namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.UI



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Annotation =
    let thickness = [1.0; 2.0; 3.0; 4.0; 5.0; 1.0; 1.0]
    let color = [new C4b(241,238,246); new C4b(189,201,225); new C4b(116,169,207); new C4b(43,140,190); new C4b(4,90,141); new C4b(241,163,64); new C4b(153,142,195) ]

    let thickn = {
        value   = 3.0
        min     = 1.0
        max     = 8.0
        step    = 1.0
        format  = "{0:0}"
    }

    let initial  = 
        {     
            geometry = GeometryType.Line
            semantic = Semantic.initial
            points = plist.Empty
            segments = plist.Empty //[]
            projection = Projection.Viewpoint
            visible = true
            text = ""
        }

    let view (mAnno : MAnnotation) = 
        let c = (Mod.force (mAnno.semantic.style)).color
        let bgc = sprintf "background: %s" (Html.ofC4b C4b.Black)     
        div [clazz "item"; style bgc] [
                i [clazz "medium File Outline middle aligned icon"][]
                text ((Mod.force (mAnno.geometry)).ToString())
        ]                                                     
        
