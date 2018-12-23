namespace GeoJsonViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

open FSharp.Data
open FSharp.Data.JsonExtensions
open Aardvark.Application

type FeatureId = FeatureId of string

type Message = 
  | Camera       of FreeFlyController.Message
  | KeyUp        of key : Keys
  | KeyDown      of key : Keys
  | Select       of string
  | Deselect
  | UpdateConfig of DockConfig


type Typus = 
  | FeatureCollection
  | Feature
  | Polygon
  | Point


type MAHLI_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type FrontHazcam_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type Mastcam_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type APXS_Properties =
  {
    id        : FeatureId
  }

type Properties =
  | MAHLI       of MAHLI_Properties
  | FrontHazcam of FrontHazcam_Properties
  | Mastcam     of Mastcam_Properties
  | APXS        of APXS_Properties
  member this.id =
    match this with
    | MAHLI       k -> k.id
    | FrontHazcam k -> k.id
    | Mastcam     k -> k.id
    | APXS        k -> k.id


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
    name : string
    typus       : Typus
    boundingBox : Box2d    
    features    : plist<Feature>
  }

[<DomainType>]
type Model = 
  {
     camera   : CameraControllerState
     data     : FeatureCollection
     docking  : DockConfig
     selected : option<string>
  }