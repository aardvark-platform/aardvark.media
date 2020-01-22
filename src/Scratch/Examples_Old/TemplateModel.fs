namespace Examples.TemplateModel

open Aardvark.Base                 
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

[<ModelType>]
type Model = { camera : CameraControllerState }

type Message = 
    CameraMessage of CameraControllerMessage