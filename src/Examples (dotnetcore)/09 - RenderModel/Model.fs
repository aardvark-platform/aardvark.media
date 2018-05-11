namespace RenderModel

open Aardvark.Base             // for math such as V3d
open Aardvark.Base.Incremental // for Mods etc and [<DomainType>]
open Aardvark.Base.Rendering   // for render attribs such as cullMode
open Aardvark.UI.Primitives    // for primitives such as camera controller state

[<DomainType>] // records can be marked as domaintypes
type Appearance = { 
    cullMode : CullMode 
}

[<DomainType>] // but also union types can be automatically mapped to adaptive versions
type Object =
    | FileModel   of string
    | SphereModel of center : V3d * radius : float // union fields can be named also
    | BoxModel    of Box3d

[<DomainType>]
type Model = { // the final domain type for our first component
    trafo         : Trafo3d 
    currentModel  : Option<Object>
    appearance    : Appearance
    cameraState   : CameraControllerState
}