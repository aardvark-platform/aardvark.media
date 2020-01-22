namespace CorrelationDrawing

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.UI
open CorrelationUtilities


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Annotation =
    let thickness = [1.0; 2.0; 3.0; 4.0; 5.0;]
//    //let color = [new C4b(241,238,246); new C4b(189,201,225); new C4b(116,169,207); new C4b(43,140,190); new C4b(4,90,141); new C4b(241,163,64); new C4b(153,142,195) ]
//
//    let thickn = {
//        value   = 3.0
//        min     = 1.0
//        max     = 8.0
//        step    = 1.0
//        format  = "{0:0}"
//    }
    
    let initial  = 
        {     
            geometry = GeometryType.Line
            semantic = Semantic.initial
            points = plist.Empty
            segments = plist.Empty //[]
            projection = Projection.Viewpoint
            visible = true
            text = "text"
        }

    type Action =
        | ChangeSemantic

    let update (anno : Annotation) (a : Action) =
        match a with
            | ChangeSemantic -> anno

    let colorInput (anno : Annotation) = 
        (Annotation.Lens.semantic |. Semantic.Lens.style |. Style.Lens.color).Get anno

    let color (anno : Annotation) =
        (Annotation.Lens.semantic |. Semantic.Lens.style |. Style.Lens.color |. ColorInput.Lens.c).Get anno


    let view (mAnno : MAnnotation) = 
        let getHtmlColor (ma : MAnnotation) = 
            adaptive {
                let! bgc = mAnno.semantic.style.color.c //mAnno.semantic.style.color.c
                return ColorPicker.colorToHex bgc // |> UI.map Semantic.Action
            }

        //let iconNode = 
            //adaptive {
            // works: attribute "style" "background: blue"
                // (style (sprintf "background: %s" (col)))
                // attribute "style" (sprintf "background: %s" (getHtmlColor mAnno))
                //div [clazz "item"; style "color: blue"] [label [clazz "ui label"][clazz ]]
         //       div [clazz "item"; style "color: blue"] [i [clazz "medium File Outline middle aligned icon"][]]
            //}
        
        let buttonTextNode =
            adaptive {
                let! lbl = mAnno.semantic.label
                let! col = (getHtmlColor mAnno)
                return div [clazz "item"] [label [clazz "ui label"; style (sprintf "background: #%s" col); onMouseClick (fun _ -> ChangeSemantic)] [text lbl]]
                // return div [clazz "item"] [button [clazz "ui button"; onMouseClick (fun _ -> ChangeSemantic)] [text lbl]]
            }

        Incremental.div AttributeMap.empty (
            alist {
                let! col = getHtmlColor mAnno
                //let! icon = iconNode
                let! button = buttonTextNode
                yield div [clazz "ui horizontal list"] [button]
            }
        )
        
        
        
                                               
        

