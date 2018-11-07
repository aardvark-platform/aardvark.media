namespace GeoJsonViewer

open Aardvark.Base
open FSharp.Data
open FSharp.Data.JsonExtensions

module GeoJSON =
  
  type EoxModel = JsonProvider<"..\..\..\data\eox.json">

  let load (jsonFile : string) : Model = 
    let eox = EoxModel.Load(jsonFile)
            
    Log.line "%A %A %A" eox.Bbox eox.Type eox.Features

    failwith ""



