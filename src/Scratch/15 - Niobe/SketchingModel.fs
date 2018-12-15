namespace Niobe.Sketching

open Aardvark.Base
open Aardvark.Base.Incremental

type SketchingAction = 
  | AddPoint of V3d
  | Undo
  | Redo

[<DomainType>]
type Brush =
  {
    points : plist<V3d>
    color  : C4b
  }

[<DomainType>]
type SketchingModel = 
  {
    working : option<Brush>
    future  : option<SketchingModel>
    past    : option<SketchingModel>
  }

module Initial = 
  let sketchingModel = 
    {
      working = None
      future = None
      past = None  
    }

