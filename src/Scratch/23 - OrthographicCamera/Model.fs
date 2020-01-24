namespace OrthoCamera

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

[<ModelType>]
type OrthoModel =
    {
       // frustum         : Frustum        
        cameraState     : CameraControllerState
        point           : V3d
    }