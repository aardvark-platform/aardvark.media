namespace SimpleScaleModel

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open PRo3DModels
open Aardvark.UI

[<ModelType>]
type Model =
    {
        camera          : CameraControllerState
        rendering       : RenderingParameters
        scale           : V3dInput
    }