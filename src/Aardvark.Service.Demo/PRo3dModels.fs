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

[<DomainType>]
type Annotation = {
    //seqNumber : int
    geometry : Geometry
    points : Points
    segments : list<Segment>
    color : C4b
    thickness : NumericInput
    projection : Projection
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
type DrawingAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    draw    : bool 
    hoverPosition : option<Trafo3d>
    points : list<V3d>

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


module InitValues = 
    let edge = [ V3d.IOI; V3d.III; V3d.OOI ]

    let annotation = 
        {
            geometry = Geometry.Polyline
            points = edge
            segments = [ edge; edge; edge ]
            color = C4b.Red
            thickness = Numeric.init
            projection = Projection.Viewpoint
            visible = true
            text = "my favorite annotation"
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