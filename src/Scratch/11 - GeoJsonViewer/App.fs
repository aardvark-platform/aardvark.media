namespace GeoJsonViewer

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module App =
  let update (model : Model) (msg : Message) =
      match msg with
          Inc -> model
  
  let view (model : MModel) =
      div [] [
          text "Hello World"
          br []
          button [onClick (fun _ -> Inc)] [text "Increment"]
          br []        
      ]
  
  
  let threads (model : Model) = 
      ThreadPool.empty
  
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
      {
          unpersist = Unpersist.instance     
          threads = threads 
          initial = 
            { 
               boundingBox = Box2d.Invalid
               typus       = Typus.Feature
               features    = list.Empty
            }
          update = update 
          view = view
      }
  