namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | Camera of FreeFlyController.Message

[<DomainType>]
type Model = 
    {
        value : int
        cameraState : CameraControllerState
    }

type Object = { trafo : IMod<string> }
type Scene = { objects : aset<Object> }

[<DomainType>]
type IObject = { itrafo : string }
[<DomainType>]
type IScene = { iobjects : hset<IObject> }