namespace Niobe.Sketching

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

type SketchingAction = 
  | AddPoint of V3d
  | ClosePolygon
  | ChangeColor  of C4b
  | SetThickness of Numeric.Action
  | SetOffset of Numeric.Action
  | SetDepthOffset of Numeric.Action
  | SetAlphaArea of Numeric.Action
  | Undo
  | Redo

[<ModelType>]
type Brush =
  {
    points : IndexList<V3d>
    color  : C4b    
  }

[<ModelType>]
type SketchingModel = 
  {
    working : option<Brush>
    finishedBrushes : IndexList<Brush>
    selectedColor : C4b
    selectedThickness : NumericInput
    volumeOffset : NumericInput
    depthOffset : NumericInput
    alphaArea : NumericInput
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

  let offset =
    {
     min = 0.1
     max = 20.0
     value = 1.0
     step = 0.1
     format = "{0:0.00}"
    }

  let depthOffset =
    {
     min = -500.0
     max = 500.0
     value = 0.001
     step = 0.001
     format = "{0:0.000}"
    }

  let alpha =
    {
     min = 0.0
     max = 1.0
     value = 0.6
     step = 0.05
     format = "{0:0.00}"
    }

  let sketchingModel = 
    {
      working = None
      future = None
      past = None  
      selectedColor = C4b.VRVisGreen
      selectedThickness = thickness
      volumeOffset = offset
      depthOffset = depthOffset
      alphaArea = alpha
      finishedBrushes = IndexList.empty
    }

