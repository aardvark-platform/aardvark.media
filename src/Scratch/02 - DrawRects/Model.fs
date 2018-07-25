namespace DrawRects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives


type Direction = Vertical | Horizontal

type ColoredRect = {
    c00 : C4f
    c10 : C4f
    c11 : C4f
    c01 : C4f
}

type Color = 
    | Gradient of direction : Direction * f : C4f * t : C4f
    | Points   of ColoredRect
    | Constant of C4f

[<DomainType>]
type Rect = {
    p00 : V2d
    p10 : V2d
    p11 : V2d
    p01 : V2d
    color : Color
    id : int
}

module Rect =
    open System.Threading
    let mutable id = 0
    let ofBox (b : Box2d) =
        let newId = Interlocked.Increment(&id)
        //let initialColor = { c00 = C4f.White; c10 = C4f.White; c11 = C4f.White; c01 = C4f.White }
        { p00 = b.Min; p10 = b.Min + V2d(b.Size.X,0.0); p11 = b.Max; p01 = b.Min + V2d(0.0,b.Size.Y); color = Constant C4f.White; id = newId }

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

[<DomainType>]
type ClientState =
    {
        viewport     : Box2d
        selectedRect : Option<int>
        workingRect  : Option<Box2d>
        currentInteraction : Interaction
    }