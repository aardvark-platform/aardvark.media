namespace Examples.TemplateModel

open Aardvark.Base                 
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

[<DomainType>]
type Model = { camera : CameraControllerState }

type Message = 
    CameraMessage of CameraControllerMessage