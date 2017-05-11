namespace PRo3DModels

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.UI.Mutable
open Aardvark.UI
open FShade.Primitives
open Demo
open Demo.TestApp

[<DomainType>]
type Bookmark = {
    id          : string
    point       : V3d
    color       : C4b
    camState    : CameraControllerState
    visible     : bool
    text        : string
}



type BoxPropertiesAction =
    | ChangeColor of int

type RenderingPropertiesAction =
        | SetFillMode of FillMode
        | SetCullMode of CullMode

[<DomainType>]
type RenderingParameters = {
    fillMode : FillMode
    cullMode : CullMode
}

type NavigationMode =
        | FreeFly = 0
        | ArcBall = 1

[<DomainType>]
type NavigationParameters = {
    navigationMode : NavigationMode    
}

[<DomainType>]
type BookmarkAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    draw    : bool 
    hoverPosition : option<Trafo3d>
    bookmarks : list<Bookmark>
}

[<DomainType>]
type VisibleBox = {
    geometry : Box3d
    color    : C4b    

    [<TreatAsValue;PrimaryKey>]
    id       : string
}

type Points = list<V3d>
type Segment = Points

type Projection = Linear = 0 | Viewpoint = 1 | Sky = 2
type Geometry = Point = 0 | Line = 1 | Polyline = 2 | Polygon = 3
type Semantic = Horizon0 = 0 | Horizon1 = 1 | Horizon2 = 2 | Horizon3 = 3 | Horizon4 = 4 | Crossbed = 5 | GrainSize = 6

[<DomainType>]
type Annotation = {
    //seqNumber : int
    geometry : Geometry
    projection : Projection
    semantic : Semantic

    points : Points
    segments : list<Segment>
    color : C4b
    thickness : NumericInput

    visible : bool
    text : string
}


[<DomainType>]
type ComposedViewerModel = {
    camera : CameraControllerState
    singleAnnotation : Annotation
    rendering : RenderingParameters

    //boxes : list<VisibleBox>
    boxHovered : option<string>   
}

type BoxSelectionDemoAction =
        | CameraMessage    of CameraControllerMessage     
        | RenderingAction  of RenderingPropertiesAction
        | Select of string     
        | Enter of string
        | Exit  
        | AddBox
        | RemoveBox
        | ClearSelection

[<DomainType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState    
    rendering : RenderingParameters

    boxes : plist<VisibleBox>
    boxesSet : hset<VisibleBox>
    boxesMap : hmap<string,VisibleBox>

    boxHovered : option<string>
    selectedBoxes : hset<string>
}


type Style = {
    color : C4b
    thickness : NumericInput
}
                
type OpenPolygon = {
    cursor : Option<V3d>
    finishedPoints : list<V3d>
    finishedSegments : list<Segment>
}


[<DomainType>]
type SimpleDrawingAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    draw    : bool 
    hoverPosition : option<Trafo3d>
    points : list<V3d>

}

[<DomainType>]
type DrawingAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    draw    : bool 
    hoverPosition : option<Trafo3d>
    //points : list<V3d>

    working : option<Annotation>
    projection : Projection
    geometry : Geometry
    semantic : Semantic

    annotations : list<Annotation>
}


[<DomainType>]
type OrbitCameraDemoModel = {
    camera : CameraControllerState
    rendering : RenderingParameters    
}

[<DomainType>]
type NavigationModeDemoModel = {
    camera : CameraControllerState
    rendering : RenderingParameters
    navigation : NavigationParameters
}

module Annotation =
    let colorsBlue = [new C4b(241,238,246); new C4b(189,201,225); new C4b(116,169,207); new C4b(43,140,190); new C4b(4,90,141)]

    let make (projection) (geometry) (semantic) : Annotation  = 
        let color = colorsBlue.[int semantic]
        {
            geometry = geometry
            semantic = semantic
            points = []
            segments = []
            color = color
            thickness = Numeric.init
            projection = projection
            visible = true
            text = ""
        }

module InitValues = 
    let edge = [ V3d.IOI; V3d.III; V3d.OOI ]

    let annotation = 
        {
            geometry = Geometry.Polyline
            points = edge
            semantic = Semantic.Horizon0
            segments = [ edge; edge; edge ]
            color = C4b.Red
            thickness = Numeric.init
            projection = Projection.Viewpoint
            visible = true
            text = "my favorite annotation"
        }

    let annotationEmpty = 
        {
            geometry = Geometry.Polyline
            semantic = Semantic.Horizon0
            points = []
            segments = []
            color = C4b.Red
            thickness = Numeric.init
            projection = Projection.Viewpoint
            visible = true
            text = "my snd favorite annotation"
        }
    
    let rendering =
        {
            fillMode = FillMode.Fill
            cullMode = CullMode.None
        }

    let navigation =
        {
            navigationMode = NavigationMode.FreeFly
        }