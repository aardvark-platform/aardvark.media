namespace GeoJsonViewer

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module App =
  let update (model : Model) (msg : Message) : Model =
      match msg with
          Inc -> model
  
  let view (model : MModel) =

      let content =
        alist {
          for f in model.data.features do
            let t = f.properties.id |> sprintf "Hello %A"
            yield div[] [text t]
        }

      Incremental.div AttributeMap.empty content
      
  let threads (model : Model) = 
      ThreadPool.empty
  
  let initialData = 
    { 
      boundingBox = Box2d.Invalid
      typus       = Typus.Feature
      features    = PList.empty
    }
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         
    {
        unpersist = Unpersist.instance     
        threads   = threads 
        initial   = { data = GeoJSON.load @"..\..\..\data\eox.json" }
        update    = update 
        view      = view
    }
  