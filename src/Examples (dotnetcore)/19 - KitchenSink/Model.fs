namespace RenderControl.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | SetFloat  of float
    | SetInt    of int
    | SetString of string 

[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState


        floatValue  : float
        intValue    : int
        stringValue : string


    }