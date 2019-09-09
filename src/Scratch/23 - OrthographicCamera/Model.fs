namespace OrthoCamera

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives


[<DomainType>]
type OrthoModel =
    {
       // frustum         : Frustum        
        cameraState     : CameraControllerState
        point           : V3d
    }