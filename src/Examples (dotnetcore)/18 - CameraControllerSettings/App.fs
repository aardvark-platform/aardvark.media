module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open RenderControl.Model

open Aardvark.Base.LensOperators

let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m; rot = model.rot + 0.01 }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | SetLookAtSensitivity s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.lookAtMouseSensitivity <== (model,s)
        | SetLookAtConstant    s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.lookAtConstant <== (model,s)
        | SetLookAtSmoothing   s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.lookAtDamping  <== (model,s)
                                  
        | SetPanSensitiviy     s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.panMouseSensitivity <== (model,s)
        | SetPanConstant       s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.panConstant <== (model,s)
        | SetPanSmoothing      s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.panDamping  <== (model,s)
                                
        | SetDollySensitiviy   s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.dollyMouseSensitivity <== (model,s)
        | SetDollyConstant     s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.dollyConstant <== (model,s)
        | SetDollySmoothing    s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.dollyDamping  <== (model,s)
          
        | SetZoomSensitiviy    s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.zoomMouseWheelSensitivity <== (model,s)
        | SetZoomConstant      s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.zoomConstant <== (model,s)
        | SetZoomSmoothing     s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.zoomDamping  <== (model,s)

        | SetMoveSensitivity   s -> Model.Lens.cameraState |. CameraControllerState.Lens.freeFlyConfig |. FreeFlyConfig.Lens.moveSensitivity <== (model,s)
        | SetTime -> { model with rot = 0.01 + model.rot }
       


let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.trafo (model.rot |> Mod.map (fun a -> Trafo3d.RotationZ a))
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }


let view (model : MModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 100%; height:100%"; 
                                           attribute "showFPS" "true";       // optional, default is false
                                           attribute "data-samples" "8"
                                           //onEvent "onRendered" [] (fun _ -> SetTime)
                                         ]) 
                    (viewScene model)


    body [style "background-color: white"] [
        div [style "display: flex; flex-direction: row; width: 100%; height: 100%" ] [
            div [style "width:70%; height:100%"] [
                renderControl
            ]
            div [style "width:30%;height:100%;"] [
                button [onClick (fun _ -> CenterScene)] [text "center scene"]
                br []
                br []
                Simple.labeledFloatInput "lookAtMouseSensitiviy" 0.0 1.0 0.001 SetLookAtSensitivity model.cameraState.freeFlyConfig.lookAtMouseSensitivity
                Simple.labeledFloatInput "lookAtConstant"        0.0 1.0 0.001 SetLookAtConstant    model.cameraState.freeFlyConfig.lookAtConstant
                Simple.labeledFloatInput "lookAtDamping  "       0.0 100.0 1.0 SetLookAtSmoothing   model.cameraState.freeFlyConfig.lookAtDamping 
                                                                                               
                Simple.labeledFloatInput "panMouseSensitivity"   0.0 1.0 0.001 SetPanSensitiviy     model.cameraState.freeFlyConfig.panMouseSensitivity
                Simple.labeledFloatInput "panConstant"           0.0 1.0 0.001 SetPanConstant       model.cameraState.freeFlyConfig.panConstant
                Simple.labeledFloatInput "panDamping"            0.0 10.0 0.10 SetPanSmoothing      model.cameraState.freeFlyConfig.panDamping 
                                                                                                
                Simple.labeledFloatInput "dollyMouseSensitivity" 0.0 1.0 0.001 SetDollySensitiviy   model.cameraState.freeFlyConfig.dollyMouseSensitivity
                Simple.labeledFloatInput "dollyConstant"         0.0 1.0 0.001 SetDollyConstant     model.cameraState.freeFlyConfig.dollyConstant
                Simple.labeledFloatInput "dollyDamping"          0.0 10.00 0.1 SetDollySmoothing    model.cameraState.freeFlyConfig.dollyDamping 
                                                                                               
                Simple.labeledFloatInput "zooomAtMouseSensitiviy" 0.0 5.0 0.001 SetZoomSensitiviy    model.cameraState.freeFlyConfig.zoomMouseWheelSensitivity
                Simple.labeledFloatInput "zooomAtConstant"        0.0 1.0 0.001 SetZoomConstant      model.cameraState.freeFlyConfig.zoomConstant
                Simple.labeledFloatInput "zooomAtDamping"         0.0 10.0 0.10 SetZoomSmoothing     model.cameraState.freeFlyConfig.zoomDamping 
                                                                                               
                Simple.labeledFloatInput "moveSensitivity"       0.0 10.0 0.01 SetMoveSensitivity   model.cameraState.freeFlyConfig.moveSensitivity
            ]
        ]
    ]

let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
               rot = 0.0
            }
        update = update 
        view = view
    }
