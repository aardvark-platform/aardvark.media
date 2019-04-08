namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | Camera of FreeFlyController.Message


[<DomainType>]
type Model = {
    value : int
    cameraState : CameraControllerState
}

type MasterMessage =
    | ResetAll
    | Nop

[<DomainType>]
type MasterModel = 
    {
        clients : hmap<string,int>
    }

