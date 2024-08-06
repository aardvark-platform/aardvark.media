[<AutoOpen>]
module Helpers

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

open Aardvark.UI

module Sg =

   module Assimp =
        let loadFromFile zUp f =  
            f |> Aardvark.SceneGraph.Assimp.Loader.Assimp.load 
              |> Sg.adapter 
              |> Sg.noEvents
              // z up transform (optionally)
              //|> Sg.transform (if zUp then Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO) else Trafo3d.Identity)
