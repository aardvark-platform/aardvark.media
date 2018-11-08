namespace GeoJsonViewer

open Aardvark.Base
open FSharp.Data
open FSharp.Data.JsonExtensions

module GeoJSON =

  let load (jsonFile : string) : Model = 
    let root = JsonValue.Load(jsonFile)
    
    match root with 
    | JsonValue.Record r -> ()
      
      //let typus, boundingBox, features = root?type, info?bbox, info?features
      //Log.line "%A" r
    | _ -> Log.line "%A" root
    
    { value = 0 }



