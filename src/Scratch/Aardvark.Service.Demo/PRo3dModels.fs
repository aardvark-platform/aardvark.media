namespace PRo3DModels

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.UI.Mutable
open Aardvark.UI
open Aardvark.UI.Primitives
open FShade.Primitives
open Demo
open Demo.TestApp
open System.Net


[<ModelType>]
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

[<ModelType>]
type RenderingParameters = {
    fillMode : FillMode
    cullMode : CullMode
}

type NavigationMode =
        | FreeFly = 0
        | ArcBall = 1

[<ModelType>]
type NavigationParameters = {
    navigationMode : NavigationMode    
}

[<ModelType>]
type BookmarkAppModel = {
    bookmarkCamera    : CameraControllerState
    rendering : RenderingParameters

    draw          : bool 
    hoverPosition : Option<Trafo3d>
    boxHovered    : Option<string>
    bookmarks     : IndexList<Bookmark>
}

[<ModelType>]
type VisibleBox = {
    geometry : Box3d
    color    : C4b    

    [<TreatAsValue;PrimaryKey>]
    id       : string
}

type Points = list<V3d>

type Segment = Points

type Projection = Linear = 0 | Viewpoint = 1 | Sky = 2
type Geometry = Point = 0 | Line = 1 | Polyline = 2 | Polygon = 3 | DnS = 4 | Undefined = 5
type Semantic = Horizon0 = 0 | Horizon1 = 1 | Horizon2 = 2 | Horizon3 = 3 | Horizon4 = 4 | Crossbed = 5 | GrainSize = 6


[<ModelType>]
type Annotation = {
    
    geometry : Geometry
    projection : Projection
    semantic : Semantic

    points : IndexList<V3d>
    segments : IndexList<IndexList<V3d>> //list<Segment>
    color : C4b
    thickness : NumericInput

    visible : bool
    text : string
}

[<ModelType>]
type MeasurementsImporterAppModel = {
    measurementsCamera : CameraControllerState
    measurementsRendering : RenderingParameters

    measurementsHoverPosition : option<Trafo3d>

    scenePath     : string
    annotations : IndexList<Annotation>
}

[<ModelType>]
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

[<ModelType>]
type BoxSelectionDemoModel = {
    camera : CameraControllerState    
    rendering : RenderingParameters

    boxes : IndexList<VisibleBox>
    boxesSet : HashSet<VisibleBox>
    boxesMap : HashMap<string,VisibleBox>

    boxHovered : option<string>
    selectedBoxes : HashSet<string>
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


[<ModelType>]
type SimpleDrawingAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    draw    : bool 
    hoverPosition : option<Trafo3d>
    points : list<V3d>

}


[<ModelType>]
type DrawingModel = {

    draw    : bool 
    hoverPosition : option<Trafo3d>

    working : Option<Annotation>
    projection : Projection
    geometry : Geometry
    semantic : Semantic

    annotations : IndexList<Annotation>
    exportPath : string
}


[<ModelType>]
type AnnotationAppModel = {
    camera : CameraControllerState
    rendering : RenderingParameters

    drawing : DrawingModel
    //draw    : bool 
    //hoverPosition : option<Trafo3d>
    ////points : list<V3d>

    //working : Option<Annotation>
    //projection : Projection
    //geometry : Geometry
    //semantic : Semantic

    //annotations : IndexList<Annotation>

    [<TreatAsValue>]
    history : Option<AnnotationAppModel> 

    [<TreatAsValue>]
    future : Option<AnnotationAppModel> 
}

module JsonTypes =
    type _V3d = {
        X : double
        Y : double
        Z : double
    }

    type _Points = list<_V3d>

    type _Segment = list<_V3d>

    type _Annotation = {       
        semantic : string
        geometry : _Points 
        segments : list<_Segment>
        color : string
        thickness : double        
        projection : string
        elevation : double
        distance : double
    }

    let ofV3d (v:V3d) : _V3d = { X = v.X; Y = v.Y; Z = v.Z }

    let ofPolygon (p:Points) : _Points = p  |> List.map ofV3d

    let ofSegment (s:Segment) : _Segment = s  |> List.map ofV3d

    let ofSegment1 (s:IndexList<V3d>) : _Segment = s  |> IndexList.map ofV3d
                                                  |> IndexList.toList


    let rec fold f s xs =
        match xs with
            | x::xs -> 
                    let r = fold f s xs
                    f x r
            | [] -> s

    let sum = [ 1 .. 10 ] |> List.fold (fun s e -> s * e) 1

    let sumDistance (polyline : Points) : double =
        polyline  |> List.pairwise |> List.fold (fun s (a,b) -> s + (b - a).LengthSquared) 0.0 |> Math.Sqrt

    let ofAnnotation (a:Annotation) : _Annotation =
        let polygon = ofPolygon (a.points |> IndexList.toList)
        let avgHeight = (polygon |> List.map (fun v -> v.Z ) |> List.sum) / double polygon.Length
        let distance = sumDistance (a.points |> IndexList.toList)
        {            
            semantic = a.semantic.ToString()
            geometry = polygon
            segments = a.segments |> IndexList.map (fun x -> ofSegment1 x) |> IndexList.toList //|> List.map (fun x -> ofSegment x)
            color = a.color.ToString()
            thickness = a.thickness.value
            
            projection = a.projection.ToString()
            elevation = avgHeight
            distance = distance
        }  

    //let ofDrawing (m : Drawing) : list<_Annotation> =
    //    m.finished.AsList |> List.map ofAnnotation

[<ModelType>]
type OrbitCameraDemoModel = {
    camera          : CameraControllerState
    rendering       : RenderingParameters    
    orbitCenter     : V3dInput
    color           : ColorInput
    navsensitivity  : NumericInput
}

[<ModelType>]
type NavigationModeDemoModel = {
    camera : CameraControllerState
    rendering : RenderingParameters
    navigation : NavigationParameters
    navsensitivity : NumericInput
    zoomFactor : NumericInput
    panFactor : NumericInput
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Annotation =
    let thickness = [1.0; 2.0; 3.0; 4.0; 5.0; 1.0; 1.0]
    let color = [new C4b(241,238,246); new C4b(189,201,225); new C4b(116,169,207); new C4b(43,140,190); new C4b(4,90,141); new C4b(241,163,64); new C4b(153,142,195) ]

    let thickn = {
        value   = 3.0
        min     = 1.0
        max     = 8.0
        step    = 1.0
        format  = "{0:0}"
    }

    let make (projection) (geometry) (semantic) : Annotation  = 
        let thickness = thickness.[int semantic]
        let color = color.[int semantic]
        {
            
            geometry = geometry
            semantic = semantic
            points = plist.Empty
            segments = plist.Empty //[]
            color = color
            thickness = { thickn with value = thickness}
            projection = projection
            visible = true
            text = ""
        }

[<ModelType>]
type FalseColorsModel = {
    useFalseColors  : bool
    lowerBound      : NumericInput
    upperBound      : NumericInput
    interval        : NumericInput
    invertMapping   : bool
    lowerColor      : ColorInput //C4b
    upperColor      : ColorInput //C4b
}

module InitValues = 
    let edge = [ V3d.IOI; V3d.III; V3d.OOI ]
    let annotation = 
        {
            geometry = Geometry.Polyline
            points = edge |> IndexList.ofList
            semantic = Semantic.Horizon0
            segments = IndexList.ofList [edge |> IndexList.ofList; edge |> IndexList.ofList; edge |> IndexList.ofList] //[edge; edge; edge]
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
            points = IndexList.empty
            segments = IndexList.empty //[]
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

    let lb  = {
        value   = 0.0
        min     = 0.0
        max     = 100.0
        step    = 1.0
        format  = "{000:0}"
    } 
    let ub  = {
        value   = 100.0
        min     = 0.0
        max     = 100.0
        step    = 1.0
        format  = "{000:0}"
    } 
    let interv  = {
        value   = 5.0
        min     = 0.0
        max     = 100.0
        step    = 1.0
        format  = "{000:0}"
    } 
    let falseColors = 
        {
            useFalseColors  = true
            lowerBound      = lb
            upperBound      = ub
            interval        = interv
            invertMapping   = true
            lowerColor      = { c = C4b.Blue }
            upperColor      = { c = C4b.Red }
        }

