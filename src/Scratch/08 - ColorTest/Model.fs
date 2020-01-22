namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

type Message = 
    | Inc
    | Camera of FreeFlyController.Message

[<ModelType>]
type Model = 
    {
        value : int
        cameraState : CameraControllerState
    }

[<ReferenceEquality;>]
type Object = { trafo : aval<string> }
[<ReferenceEquality;>]
type Scene = { objects : aset<Object> }

[<ModelType>]
type IObject = { itrafo : string }
[<ModelType>]
type IScene = { iobjects : CountingHashSet<IObject> }