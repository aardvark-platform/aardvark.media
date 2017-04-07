namespace Scratch.DomainTypes

open System
open Fablish
open Aardvark.Base
open Aardvark.Base.Incremental


module Camera =
    open Aardvark.Base
    open Aardvark.Base.Rendering
    
    type NavigationMode = FreeFly | Orbital

    [<DomainType>]
    type Model = { 
        camera : CameraView
        //frustum : Frustum 
        lookingAround : Option<PixelPosition>
        panning : Option<PixelPosition>
        zooming : Option<PixelPosition>
        picking : Option<int>
        center : Option<V3d>
        navigationMode : NavigationMode

        forward : V2d
        forwardSpeed : float
    }

type Axis = X | Y | Z
module TranslateController =


    [<DomainType>]
    type TModel = {
        hovered           : Option<Axis>
        activeTranslation : Option<Axis*Plane3d * V3d>
        trafo             : Trafo3d
        editTrafo         : Trafo3d
    }

    [<DomainType>]
    type Scene = 
        {
            camera : Camera
            scene : TModel
        }

module RotateController =
    [<DomainType>]
    type RModel = {
        hovered           : Option<Axis>
        activeRotation    : Option<Axis*Plane3d * V3d>
        trafo             : Trafo3d
        editTrafo         : Trafo3d
    }

    [<DomainType>]
    type Scene =  {
        camera : Camera
        scene : RModel
    }

module SimpleDrawingApp =

    type Polygon = list<V3d>

    type OpenPolygon = {
        cursor         : Option<V3d>
        finishedPoints : list<V3d>
    }
    
    [<DomainType>]
    type Model = {
        finished : pset<Polygon>
        working  : Option<OpenPolygon>
    }

module DrawingApp =
    
    type Style = {    
        color : C4b
        thickness : Numeric.Model
    }

    module Default =
        let thickness = {
            value   = 1.5
            min     = 0.5
            max     = 4.0
            step    = 0.5
            format  = "{0:0.0}"
        }

        let thickness' v = { thickness with value = v } 

        let samples = {
            value   = 4.0
            min     = 1.0
            max     = 100.0
            step    = 1.0
            format  = "{0:0}"
        }

        let samples' v = { samples with value = v } 
    
    type Polygon = list<V3d>
    type Segment = list<V3d>
    
    type Annotation = {
        seqNumber : int
        annType : string
        geometry : Polygon
        segments : list<Segment>
        style : Style        
        projection : Choice.Model
    }

    type OpenPolygon = {
        cursor         : Option<V3d>
        finishedPoints : list<V3d>        
        finishedSegments : list<Segment>
    }
    
    [<DomainType>]
    type Drawing = {
        //ViewerState : Camera.Model
        future   : Option<Drawing>
        history  : Option<Drawing>
        finished : pset<Annotation>
        working  : Option<OpenPolygon>
        picking : Option<int>
        style   : Style
        measureType : Choice.Model
        projection : Choice.Model
        samples : Numeric.Model
        selected : pset<int>
        selectedAnn : Option<Annotation>
    }

module ComposeTest =

    [<DomainType>]
    type Model = {
        ViewerState : Camera.Model
        Drawing : DrawingApp.Drawing
    }

module PlaceTransformObjects =

    open TranslateController

    [<DomainType>]
    type Selected = { id : int; tmodel : TModel }

    [<DomainType>]
    type Object = { id : int; t : Trafo3d }

    [<DomainType>]
    type Model = {
        objects : pset<Object>
        hoveredObj : Option<int>
        selectedObj : Option<Selected>
    }

module Interop =
    
    type Active = RenderControl | Gui

    type Scene = {
        camera : Camera
        obj    : V3d
    }

    type Model = {
        currentlyActive : Active
        scene : Scene
    }


module SharedModel =

    open TranslateController
    
    [<DomainType>]
    type Ui = { cnt : int; info : string }

    [<DomainType>]
    type Model = { ui : Ui; scene : Scene }