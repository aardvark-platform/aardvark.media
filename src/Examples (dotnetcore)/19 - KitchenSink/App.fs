module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open RenderControl.Model

open Aardvark.UI.Primitives.Simple


let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }

        | SetFloat f -> 
            printfn "set value: %f" f
            { model with floatValue = f  }
        | SetInt d -> 
            printfn "set value: %d" d
            { model with intValue = d  }
        | SetEnum e -> 
            printfn "set enum to: %A" e
            { model with enumValue = e }
        | SetUnion e -> 
            printfn "set union to: %A" e
            { model with unionValue = e }
        | ToggleBoolean -> 
            printfn "toggle boolean old: %A" model.boolean
            { model with boolean = not model.boolean }

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let view (model : MModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera 
                    (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 100px; height:100px"; attribute "data-samples" "8"]) 
                    (viewScene model)


    div [] [
        text "Rendering"
        br []
        renderControl
        br []
        text "simple float input: "
        br []
        Html5.labeledFloatInput' AttributeMap.empty (AttributeMap.ofList [style "width:100px;display:inline-block;"]) 
            (AttributeMap.ofList [style "width:100px"])
            None "float value: " 0.0 10.0 0.01 SetFloat model.floatValue
        Html5.labeledIntegerInput' AttributeMap.empty (AttributeMap.ofList [style "width:100px;display:inline-block;"]) 
            (AttributeMap.ofList [style "width:100px"])
            None "int value:   " 0 10 SetInt model.intValue
        text "automatic dropdown for enums: "
        Html5.dropDownAuto (AttributeMap.ofList [style "width:60px"])  model.enumValue SetEnum
        br []
        text "automatic dropdown for enums: "
        Html5.dropDownAuto (AttributeMap.ofList [style "width:60px"]) model.unionValue SetUnion
        br []
        br []
        br []

        require Html.semui (
            div [clazz "ui"] [
                span [clazz "ui label"] [text "predefined semantic ui controls"]
                br []
                SemUi.labeledFloatInput "semui float input" 0.0 10.0 0.1 SetFloat model.floatValue
                br []
                SemUi.dropDownAuto AttributeMap.empty model.enumValue SetEnum 
                br []
                SemUi.toggleBox "boolean" model.boolean ToggleBoolean
                br []
                button [clazz "ui button"; onClick (fun _ -> ToggleBoolean)] [text "toggle externally"]
            ]
        )
    ]

let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map Camera


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
               floatValue = 0.5
               intValue = 1
               stringValue = "no value yet"
               enumValue = EnumValue.One
               unionValue = U
               boolean = false
            }
        update = update 
        view = view
    }
