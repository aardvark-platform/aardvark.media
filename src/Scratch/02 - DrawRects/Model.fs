namespace DrawRects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives


type Direction = Vertical | Horizontal

type ColoredRect = {
    colors : C4f[]
}

type Color = 
    | Gradient of direction : Direction * f : C4f * t : C4f
    | Points   of ColoredRect
    | Constant of C4f 

[<DomainType>]
type Rect = {
    box : Box2d
    color : Color
    id : int
}


type OpenRect = { s : V2d; t : V2d }

module Rect =
    open System.Threading
    let mutable id = 0
    let ofBox (b : OpenRect) =
        let newId = Interlocked.Increment(&id)
        //let initialColor = { c00 = C4f.White; c10 = C4f.White; c11 = C4f.White; c01 = C4f.White }
        //{ p00 = b.Min; p10 = b.Min + V2d(b.Size.X,0.0); p11 = b.Max; p01 = b.Min + V2d(0.0,b.Size.Y); color = Constant C4f.White; id = newId }
        { box = Box2d.FromPoints(b.s,b.t); color = Constant C4f.White; id = newId }

[<DomainType>]
type Model = 
    {
        rects : hmap<int,Rect>
    }

type Interaction =
    | CreatingRect
    | MovingRect
    | MovingPoint
    | Nothing
    
type DragEndpoint = { rect : int; vertexId : int; fixedPoint : V2d; pos : V2d }

[<DomainType>]
type ClientState =
    {
        viewport     : Box2d
        selectedRect : Option<int>
        workingRect  : Option<OpenRect>
        dragEndPoint : Option<DragEndpoint>
        downOnRect : bool

        dragRect : Option<V2d>

        mouseDown : Option<V2d>
        mouseDrag : Option<V2d>

        currentInteraction : Interaction
    }