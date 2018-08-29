namespace RenderControl.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene

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

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState
        rot : float
    }