namespace DragNDrop

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<V3d>
    camera      : Demo.TestApp.CameraControllerState
}