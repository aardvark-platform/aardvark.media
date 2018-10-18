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
        text "super simple float input: "
        br []
        Plain.labeledFloatInput' AttributeMap.empty AttributeMap.empty 
            "float value: " 0.0 10.0 0.01 SetFloat model.floatValue
        br []
        Plain.labeledIntegerInput' AttributeMap.empty AttributeMap.empty 
            None "int value: " 0 10 SetInt model.intValue
        br []

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
            }
        update = update 
        view = view
    }
