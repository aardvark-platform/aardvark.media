namespace RenderModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

[<DomainType>]
type Object =
    | FileModel   of string
    | SphereModel of center : V3d * radius : float
    | BoxModel    of Box3d

[<DomainType>]
type Model = { 
    trafo         : Trafo3d 
    currentModel  : Option<Object>
    cameraState   : CameraControllerState
}