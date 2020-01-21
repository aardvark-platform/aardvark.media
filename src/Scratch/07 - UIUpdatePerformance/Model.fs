namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open System

type Message = 
    | Inc
    | Go
    | Done
    | Super
    | Tick
    | GotImage of DateTime
    | Camera of FreeFlyController.Message

[<ModelType>]
type Model = 
    {
        value : int
        super : int
        threads : ThreadPool<Message>
        updateStart : float
        took : float
        things : IndexList<string>
        angle : float
        lastImage : DateTime
        cameraState : CameraControllerState
    }