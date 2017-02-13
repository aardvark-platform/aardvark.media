namespace Scratch.DomainTypes

open System
open Aardvark.Base
open Aardvark.Base.Incremental

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