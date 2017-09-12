namespace Simple2DDrawing

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Polygon = { points : list<V2d> }

[<DomainType>]
type Model =
    {
        finishedPolygons : plist<Polygon>

        workingPolygon : Option<Polygon>
    }

type Message = 
    | AddPoint of V2d
    | ClosePolygon
