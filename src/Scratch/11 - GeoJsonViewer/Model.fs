namespace GeoJsonViewer

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

open FSharp.Data
open FSharp.Data.JsonExtensions

type Message = 
    | Inc


type Typus = 
  | Feature

type Feature = int

[<DomainType>]
type Model = 
  {
    boundingBox : Box2d
    typus       : Typus
    features    : list<Feature>
  }