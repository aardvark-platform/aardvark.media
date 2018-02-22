namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives


type Projection = Linear = 0 | Viewpoint = 1 | Sky = 2
type GeometryType = Point = 0 | Line = 1 | Polyline = 2 | Polygon = 3 | DnS = 4 | Undefined = 5
type SemanticType = Metric = 0 | Angular = 1 | Hierarchical = 2


[<DomainType>]
type Style = {
    color : ColorInput
    thickness : NumericInput
 } 

[<DomainType>]
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


[<DomainType>]
type Semantic = {
        label             : string
        size              : double
        style             : Style
        geometry          : GeometryType
        semanticType      : SemanticType
    }

[<DomainType>]
type Annotation = {       
    geometry : GeometryType
    projection : Projection
    semantic : Semantic
    points : plist<V3d>
    segments : plist<plist<V3d>> //list<Segment>
    visible : bool
    text : string
}

[<DomainType>]
type Border = {
    annotations : plist<Annotation>
}

[<DomainType>]
type LogModel = {
        id      : string
        //borders : alist<Annotation>
        range   : V2d //?
        // horizon ?
}

[<DomainType>]
type CorrelationDrawingModel = {
    draw             : bool 
    hoverPosition    : option<Trafo3d>
    working          : option<Annotation>
    projection       : Projection
    geometry         : GeometryType
    semantics        : hmap<string, Semantic>
    semanticsList    : plist<Semantic>
    selectedSemantic : option<string>
    annotations      : plist<Annotation>
    exportPath       : string
}

[<DomainType>]
type CorrelationAppModel = {
    camera           : CameraControllerState
    rendering        : RenderingParameters
    drawing          : CorrelationDrawingModel

    [<TreatAsValue>]
    history          : Option<CorrelationAppModel> 

    [<TreatAsValue>]
    future           : Option<CorrelationAppModel>     
}