namespace SimpleScaleModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open PRo3DModels
open Aardvark.UI

[<DomainType>]
type Model =
    {
        camera          : CameraControllerState
        rendering       : RenderingParameters
        scale           : V3dInput
    }