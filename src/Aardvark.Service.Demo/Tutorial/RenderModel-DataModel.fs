namespace RenderModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

[<DomainType>]
type Object =
    | FileModel   of string
    | SphereModel of center : V3d * radius : float
    | BoxModel    of Box3d

type ShadingMode = Colored = 0 | Lighted = 1 | Textured = 2

[<DomainType>]
type Model = { 
    trafo         : Trafo3d 
    currentModel  : Option<Object>
    shadingMode   : ShadingMode
    cameraState   : CameraControllerState
}