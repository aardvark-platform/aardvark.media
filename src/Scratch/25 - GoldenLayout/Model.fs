﻿namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Adaptify

type Message =
    | Camera of FreeFlyController.Message
    | CenterScene
    | ToggleBackground
    | LayoutChanged
    | GoldenLayout of Golden.GoldenLayout.Message

[<ModelType>]
type Model =
    {
        cameraState : CameraControllerState
        background : C4b
        golden : Golden.GoldenLayout
    }