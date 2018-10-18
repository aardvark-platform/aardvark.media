namespace RenderControl.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type EnumValue = One = 1 | Two = 2

type UnionValue = U | O

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | SetFloat  of float
    | SetInt    of int
    | SetString of string 
    | SetEnum   of EnumValue
    | SetUnion  of UnionValue


[<DomainType>]
type Model = 
    {
        cameraState : CameraControllerState


        floatValue  : float
        intValue    : int
        stringValue : string

        enumValue : EnumValue
    
    }