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
        cursor         : Option<V2d>

        [<TreatAsValue>]
        past   : Option<Model>
        [<TreatAsValue>]
        future : Option<Model>
    }

type Message = 
    | AddPoint of V2d
    | ClosePolygon of V2d
    | MoveCursor of V2d

    | Undo of unit
    | Redo of unit