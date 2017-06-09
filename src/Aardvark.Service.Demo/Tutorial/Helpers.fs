[<AutoOpen>]
module Helpers

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

open Aardvark.UI

module Sg =

   module Assimp =
        let loadFromFile f =  
            f |> Aardvark.SceneGraph.IO.Loader.Assimp.load |> Sg.adapter |> Sg.noEvents
