namespace GeoJsonViewer

open Aardvark.Base
open FSharp.Data
open FSharp.Data.JsonExtensions

module GeoJSON =
  
  type EoxModel = JsonProvider<"../../../data/eox.json">

  let parseBoundingBox (bb : array<decimal>) : Box2d =

    if bb.Length <> 4 then failwith "invalid bounding box of size other than 4"

    let minLat = float bb.[0]
    let minLon = float bb.[1]
    let maxLat = float bb.[2]
    let maxLon = float bb.[3]

    Box2d(minLon, minLat, maxLon, maxLat)

  let parseTypus (typus : string) : Typus =
    match typus.ToLowerInvariant() with
      | "featurecollection" -> Typus.FeatureCollection
      | "feature"           -> Typus.Feature
      | "polygon"           -> Typus.Polygon
      | s -> s |> sprintf "string %A unknown" |> failwith

  let parseProperties (properties : EoxModel.Properties) : Properties = 
    {
      id        = properties.Id |> FeatureId
      beginTime = properties.BeginTime.DateTime
      endTime   = properties.EndTime.DateTime
    }
    
  let parseSingleCoord (c : array<decimal>) : V2d =
    if c.Length <> 2 then failwith "invalid coordinate of size other than 2"
    V2d(float c.[0], float c.[1])

  let parseCoordinates (coordinateSet : array<array<array<decimal>>>) : list<V2d> =
    [
      for set in coordinateSet do
        for c in set do
          yield c |> parseSingleCoord              
    ]
    
  let parseGeometry (geometry : EoxModel.Geometry) : GeoJsonViewer.Geometry = 
    {
      typus       = geometry.Type        |> parseTypus
      coordinates = geometry.Coordinates |> parseCoordinates
    }        

  let parseFeature (feature : EoxModel.Feature) : Feature =
    {
      typus       = feature.Type       |> parseTypus
      boundingBox = feature.Bbox       |> parseBoundingBox
      properties  = feature.Properties |> parseProperties
      geometry    = feature.Geometry   |> parseGeometry
    } 

  let parseFeatures (features : array<EoxModel.Feature>) : list<Feature> = 
    features |> Array.toList |> List.map parseFeature    

  let parseRoot(root : EoxModel.Root) : FeatureCollection = 
    {
      typus       = root.Type     |> parseTypus
      boundingBox = root.Bbox     |> parseBoundingBox
      features    = root.Features |> parseFeatures |> PList.ofList
    }

  let load (jsonFile : string) : FeatureCollection = 
    EoxModel.Load(jsonFile) |> parseRoot                

    



