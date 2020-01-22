namespace CorrelationDrawing

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives


type Projection = Linear = 0 | Viewpoint = 1 | Sky = 2
type GeometryType = Point = 0 | Line = 1 | Polyline = 2 | Polygon = 3 | DnS = 4 | Undefined = 5
type SemanticType = Metric = 0 | Angular = 1 | Hierarchical = 2


[<ModelType>]
type Style = {
    color : ColorInput
    thickness : NumericInput
 } 

[<ModelType>]
type RenderingParameters = {
    fillMode : FillMode
    cullMode : CullMode
}   
    
module RenderingPars =
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    let initial : RenderingParameters = {
        fillMode = FillMode.Fill
        cullMode = CullMode.None
    }


[<ModelType>]
type Semantic = {
        label             : string
        size              : double
        style             : Style
        geometry          : GeometryType
        semanticType      : SemanticType
    }

[<ModelType>]
type Annotation = {       
    geometry : GeometryType
    projection : Projection
    semantic : Semantic
    points : IndexList<V3d>
    segments : IndexList<IndexList<V3d>> //list<Segment>
    visible : bool
    text : string
}

[<ModelType>]
type Border = {
    annotations : IndexList<Annotation>
}

[<ModelType>]
type LogModel = {
        id      : string
        //borders : alist<Annotation>
        range   : V2d //?
        // horizon ?
}

[<ModelType>]
type CorrelationDrawingModel = {
    draw             : bool 
    hoverPosition    : option<Trafo3d>
    working          : option<Annotation>
    projection       : Projection
    geometry         : GeometryType
    semantics        : HashMap<string, Semantic>
    semanticsList    : IndexList<Semantic>
    selectedSemantic : option<string>
    annotations      : IndexList<Annotation>
    exportPath       : string
}

[<ModelType>]
type CorrelationAppModel = {
    camera           : CameraControllerState
    rendering        : RenderingParameters
    drawing          : CorrelationDrawingModel

    [<TreatAsValue>]
    history          : Option<CorrelationAppModel> 

    [<TreatAsValue>]
    future           : Option<CorrelationAppModel>     
}