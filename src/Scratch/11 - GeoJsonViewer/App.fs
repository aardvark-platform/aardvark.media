namespace GeoJsonViewer

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module App =
  let update (model : Model) (msg : Message) =
      match msg with
          Inc -> { model with value = model.value + 1 }
  
  let view (model : MModel) =
      div [] [
          text "Hello World"
          br []
          button [onClick (fun _ -> Inc)] [text "Increment"]
          text "    "
          Incremental.text (model.value |> Mod.map string)
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
                 value = 0
              }
          update = update 
          view = view
      }
  