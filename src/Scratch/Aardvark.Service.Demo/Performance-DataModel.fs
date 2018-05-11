namespace Performance

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives


[<DomainType>]
type Model =
    {
        visible     : plist<Trafo3d>
        objects     : plist<Trafo3d>
        cameraState : CameraControllerState
    }