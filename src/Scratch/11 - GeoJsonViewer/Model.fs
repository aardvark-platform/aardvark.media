namespace GeoJsonViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

open FSharp.Data
open FSharp.Data.JsonExtensions

type FeatureId = FeatureId of string

type Message = 
  | Select       of string
  | Deselect
  | UpdateConfig of DockConfig


type Typus = 
  | FeatureCollection
  | Feature
  | Polygon
  | Point


type Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type Geometry = 
  {
    typus       : Typus
    coordinates : list<V2d>
  }

type Feature =
  { 
    id          : string
    typus       : Typus
    properties  : Properties
    boundingBox : Box2d
    geometry    : Geometry
  }

[<DomainType>]
type FeatureCollection = 
  {
    typus       : Typus
    boundingBox : Box2d    
    features    : plist<Feature>
  }

[<DomainType>]
type Model = 
  {
     data     : FeatureCollection
     docking  : DockConfig
     selected : option<string>
  }