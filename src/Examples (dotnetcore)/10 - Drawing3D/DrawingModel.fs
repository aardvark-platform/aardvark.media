namespace DrawingModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open Aardvark.Base.Rendering

open RenderingParametersModel
open Aardvark.UI

[<DomainType>]
type SimpleDrawingModel = {
    camera     : CameraControllerState
    rendering  : RenderingParameters
    depthOffset : NumericInput

    draw          : bool 
    hoverPosition : option<Trafo3d>
    points        : list<V3d>
}
