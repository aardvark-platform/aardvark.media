namespace RenderControl.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | JumpToOrigin

    | SetLookAtSensitivity of float
    | SetLookAtConstant    of float
    | SetLookAtSmoothing   of float
                     
    | SetPanSensitiviy     of float
    | SetPanConstant       of float
    | SetPanSmoothing      of float
                     
    | SetDollySensitiviy   of float
    | SetDollyConstant     of float
    | SetDollySmoothing    of float
                      
    | SetZoomSensitiviy    of float
    | SetZoomConstant      of float
    | SetZoomSmoothing     of float
                    
    | SetMoveSensitivity   of float
    | SetTime

[<ModelType>]
type Model = 
    {
        cameraState : CameraControllerState
        rot : float
    }