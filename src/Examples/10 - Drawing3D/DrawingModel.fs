namespace DrawingModel

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.Rendering
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
