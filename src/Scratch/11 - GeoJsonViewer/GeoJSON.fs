namespace GeoJsonViewer

open Aardvark.Base
open FSharp.Data
open FSharp.Data.JsonExtensions

module GeoJSON =
  
  type EoxModel = JsonProvider<"..\..\..\data\eox.json">

  let parseBoundingBox (bb : array<decimal>) : Box2d = failwith ""
  let parseTypus (typus : string) : Typus = failwith ""
  let parseFeatures (features : array<EoxModel.Feature>) : list<Feature> = failwith ""

  let parseRoot(root : EoxModel.Root) : Model = 
    {
      boundingBox = root.Bbox     |> parseBoundingBox
      typus       = root.Type     |> parseTypus
      features    = root.Features |> parseFeatures
    }

  let load (jsonFile : string) : Model = 
    EoxModel.Load(jsonFile) |> parseRoot                

    



