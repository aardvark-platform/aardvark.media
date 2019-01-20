namespace Niobe.Sketching

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type SketchingAction = 
  | AddPoint of V3d
  | ClosePolygon
  | ChangeColor  of ColorPicker.Action
  | SetThickness of Numeric.Action
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
    selectedColor : ColorInput
    selectedThickness : NumericInput
    [<TreatAsValue>]
    future  : option<SketchingModel>
    [<TreatAsValue>]
    past    : option<SketchingModel>
  }

module Initial = 
  
  let thickness =
    {
      min = 1.0
      max = 10.0
      value = 2.0
      step = 1.0
      format = "{0}"
    }

  let sketchingModel = 
    {
      working = None
      future = None
      past = None  
      selectedColor = { c = C4b.VRVisGreen}
      selectedThickness = thickness
    }

