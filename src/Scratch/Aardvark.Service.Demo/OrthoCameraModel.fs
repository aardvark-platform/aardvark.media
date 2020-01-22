namespace OrthoCamera

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

module OrthoCameraModel =
  [<ModelType>]
  type OrthoModel =
    {
      camera     : CameraControllerState
    }