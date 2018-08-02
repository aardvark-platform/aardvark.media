namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives


[<DomainType>]
type Object =
    | Rect    of corners  : Box2d * colors : array<C4f>
    | Polygon of vertices : array<V2f> * colors : array<C4f>

module ObjectId = 
    open System.Threading
    let mutable id = 0
    let freshId() = Interlocked.Increment(&id)

[<DomainType>]
type Model = 
    {
        selectedObject  : Option<int>
        objects         : hmap<int,Object>
        cameraState     : CameraControllerState
    }