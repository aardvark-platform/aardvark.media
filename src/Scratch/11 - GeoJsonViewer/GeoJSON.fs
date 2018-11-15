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
    V2d(float c.[1], float c.[0])

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
      id          = System.Guid.NewGuid()
      typus       = feature.Type       |> parseTypus
      boundingBox = feature.Bbox       |> parseBoundingBox
      properties  = feature.Properties |> parseProperties
      geometry    = feature.Geometry   |> parseGeometry
    } 

  let parseFeatures (features : array<EoxModel.Feature>) : list<Feature> = 
    features |> Array.toList |> List.map parseFeature    

  let distanceFilter (dist : float) (features : list<Feature>) : list<Feature> =
      let boxes = features |> List.map(fun x -> x.boundingBox)

      let sum = boxes |> List.fold (fun a b -> b.Center + a) (V2d.Zero)
      let com = sum / (float boxes.Length)

      features |> List.filter(fun x -> V2d.Distance(x.boundingBox.Center, com) < dist ) //|> List.take 1

  let computeBb (features : list<Feature>) : Box2d = 
    features |> List.fold (fun a b -> a.ExtendedBy(b.boundingBox)) Box2d.Invalid

  let parseRoot(root : EoxModel.Root) : FeatureCollection = 
    let features = root.Features |> parseFeatures |> distanceFilter 10.0

    {
      typus       = root.Type     |> parseTypus
      boundingBox = features      |> computeBb //root.Bbox     |> parseBoundingBox
      features    = features      |> PList.ofList
    }

  let load (jsonFile : string) : FeatureCollection = 
    EoxModel.Load(jsonFile) |> parseRoot                

    



