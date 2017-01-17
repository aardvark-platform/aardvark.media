namespace Scratch.DomainTypes

open System
open Aardvark.Base
open Aardvark.Base.Incremental


module TranslateController =

    type Axis = X | Y | Z

    [<DomainType>]
    type Model = {
        hovered           : Option<Axis>
        activeTranslation : Option<Plane3d * V3d>
        trafo             : Trafo3d
    }

    [<DomainType>]
    type Scene = 
        {
            camera : Camera
            scene : Model
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

module PlaceTransformObjects =

    [<DomainType>]
    type Model = {
        objects : list<Trafo3d>
        hoveredObj : Option<int>
        selectedObj : Option<int * TranslateController.Model>
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
    
    [<DomainType>]
    type Ui = { cnt : int; info : string }

    [<DomainType>]
    type Model = { ui : Ui; scene : TranslateController.Scene }