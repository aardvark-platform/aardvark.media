namespace Inc.Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open System

type Direction = Vertical = 0 | Horizontal = 1


type Gradient = { direction : Direction; f : C4f; t : C4f}
type Points = { colors : array<C4f> }
type Constant = C4f

type ColorKind = 
    | Gradient = 0
    | Points   = 1
    | Constant  = 2

type Color = 
    {
         kind : ColorKind
         gradient : Gradient
         points : Points
         constant : Constant
    }

[<DomainType>]
type Object =
    | Rect    of corners  : Box2d * color : Color
    | Polygon of vertices : array<V2f> * colors : array<C4f>

module ObjectId = 
    open System.Threading
    let mutable id = 0
    let freshId() = Interlocked.Increment(&id)

type DragState = { f : V3d; t : V3d }

type DragEndpoint = { rect : int; vertexId : int; fixedPoint : V2d; pos : V2d }

[<DomainType>]
type Model = 
    {
        selectedObject  : Option<int>
        objects         : hmap<int,Object>
        cameraState     : CameraControllerState

        hoverHandle : Option<int>
        dragEndpoint : Option<DragEndpoint>
        translation : Option<V3d>
        down : Option<V3d>
        dragging : Option<DragState>
        openRect   : Option<Box3d>
        
    }