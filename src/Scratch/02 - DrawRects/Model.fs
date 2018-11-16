namespace DrawRects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

type Message = 
    | Inc

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

[<DomainType>]
type Rect = {
    p00 : V2d
    p10 : V2d
    p11 : V2d
    p01 : V2d
    color : Color
    id : int
}

[<DomainType>]
type Model = 
    {
        rects : hmap<int,Rect>
    }

[<DomainType>]
type ClientState =
    {
        viewport     : Box2d
        selectedRect : Option<int>
    }