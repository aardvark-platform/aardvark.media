namespace OrthoCamera

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

module OrthoCameraModel =
  [<DomainType>]
  type OrthoModel =
    {
      camera     : CameraControllerState
    }