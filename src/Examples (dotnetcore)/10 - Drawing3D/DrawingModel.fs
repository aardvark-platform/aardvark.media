namespace DrawingModel

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.Base.Rendering
open Adaptify

open RenderingParametersModel

[<ModelType>]
type SimpleDrawingModel = {
    camera     : CameraControllerState
    rendering  : RenderingParameters

    draw          : bool 
    hoverPosition : option<Trafo3d>
    points        : list<V3d>
}
