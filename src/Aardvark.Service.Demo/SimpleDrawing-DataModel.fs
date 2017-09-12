namespace SimpleDrawing

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type Polygon = { points : plist<Polygon> }

[<DomainType>]
type Model =
    {
        polygons : plist<Polygon>
    }
