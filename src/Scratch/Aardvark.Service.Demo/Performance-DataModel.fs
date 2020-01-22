namespace Performance

open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives


[<ModelType>]
type Model =
    {
        visible     : IndexList<Trafo3d>
        objects     : IndexList<Trafo3d>
        cameraState : CameraControllerState
    }