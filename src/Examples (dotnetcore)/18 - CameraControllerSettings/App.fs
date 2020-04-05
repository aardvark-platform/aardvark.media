module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open RenderControl.Model
open Aether.Operators

open Aardvark.Base.LensOperators

let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let rec update (model : Model) (msg : Message) =
    let bla = Model.cameraState_ >-> CameraControllerState.freeFlyConfig_ >-> FreeFlyConfig.lookAtMouseSensitivity_
    let ffc = Model.cameraState_ >-> CameraControllerState.freeFlyConfig_
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m; rot = model.rot + 0.01 }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | JumpToOrigin ->
            update model (Camera (FreeFlyController.Message.JumpTo (CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI)))
        | SetLookAtSensitivity s -> model |> s ^= (ffc >-> FreeFlyConfig.lookAtMouseSensitivity_)
        | SetLookAtConstant    s -> model |> s ^= (ffc >-> FreeFlyConfig.lookAtConstant_)
        | SetLookAtSmoothing   s -> model |> s ^= (ffc >-> FreeFlyConfig.lookAtDamping_)
                                  
        | SetPanSensitiviy     s -> model |> s ^= (ffc >-> FreeFlyConfig.panMouseSensitivity_)
        | SetPanConstant       s -> model |> s ^= (ffc >-> FreeFlyConfig.panConstant_)
        | SetPanSmoothing      s -> model |> s ^= (ffc >-> FreeFlyConfig.panDamping_)
                                
        | SetDollySensitiviy   s -> model |> s ^= (ffc >-> FreeFlyConfig.dollyMouseSensitivity_)
        | SetDollyConstant     s -> model |> s ^= (ffc >-> FreeFlyConfig.dollyConstant_)
        | SetDollySmoothing    s -> model |> s ^= (ffc >-> FreeFlyConfig.dollyDamping_)
          
        | SetZoomSensitiviy    s -> model |> s ^= (ffc >-> FreeFlyConfig.zoomMouseWheelSensitivity_)
        | SetZoomConstant      s -> model |> s ^= (ffc >-> FreeFlyConfig.zoomConstant_)
        | SetZoomSmoothing     s -> model |> s ^= (ffc >-> FreeFlyConfig.zoomDamping_)

        | SetMoveSensitivity   s -> model |> s ^= (ffc >-> FreeFlyConfig.moveSensitivity_)
        | SetTime -> { model with rot = 0.01 + model.rot }
       


let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
     //|> Sg.trafo (model.rot |> AVal.map (fun a -> Trafo3d.RotationZ a))
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }


let view (model : AdaptiveModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
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
                button [onClick (fun _ -> JumpToOrigin)] [text "Animated center"]
                br []
                br []
                Simple.labeledFloatInput "lookAtMouseSensitiviy (LMB)" 0.0 1.0 0.001 SetLookAtSensitivity model.cameraState.freeFlyConfig.lookAtMouseSensitivity
                Simple.labeledFloatInput "lookAtConstant"        0.0 1.0 0.001 SetLookAtConstant    model.cameraState.freeFlyConfig.lookAtConstant
                Simple.labeledFloatInput "lookAtDamping  "       0.0 100.0 1.0 SetLookAtSmoothing   model.cameraState.freeFlyConfig.lookAtDamping 
                                                                                               
                Simple.labeledFloatInput "panMouseSensitivity (MMB)"   0.0 1.0 0.001 SetPanSensitiviy     model.cameraState.freeFlyConfig.panMouseSensitivity
                Simple.labeledFloatInput "panConstant"           0.0 1.0 0.001 SetPanConstant       model.cameraState.freeFlyConfig.panConstant
                Simple.labeledFloatInput "panDamping"            0.0 10.0 0.10 SetPanSmoothing      model.cameraState.freeFlyConfig.panDamping 
                                                                                                
                Simple.labeledFloatInput "dollyMouseSensitivity (RMB)" 0.0 1.0 0.001 SetDollySensitiviy   model.cameraState.freeFlyConfig.dollyMouseSensitivity
                Simple.labeledFloatInput "dollyConstant"         0.0 1.0 0.001 SetDollyConstant     model.cameraState.freeFlyConfig.dollyConstant
                Simple.labeledFloatInput "dollyDamping"          0.0 10.00 0.1 SetDollySmoothing    model.cameraState.freeFlyConfig.dollyDamping 
                                                                                               
                Simple.labeledFloatInput "zooomAtMouseSensitiviy (wheel)" 0.0 5.0 0.001 SetZoomSensitiviy    model.cameraState.freeFlyConfig.zoomMouseWheelSensitivity
                Simple.labeledFloatInput "zooomAtConstant"        0.0 1.0 0.001 SetZoomConstant      model.cameraState.freeFlyConfig.zoomConstant
                Simple.labeledFloatInput "zooomAtDamping"         0.0 10.0 0.10 SetZoomSmoothing     model.cameraState.freeFlyConfig.zoomDamping 
                                                                                               
                Simple.labeledFloatInput "moveSensitivity (wasd)"       0.0 10.0 0.01 SetMoveSensitivity   model.cameraState.freeFlyConfig.moveSensitivity
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
