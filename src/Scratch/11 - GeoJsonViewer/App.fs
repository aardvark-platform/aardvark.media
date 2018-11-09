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

  let semui = 
    [ 
        { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
        { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
    ]
  
  let view (model : MModel) =
                   
      let content =
        alist {
          for f in model.data.features do

          let (FeatureId id) = f.properties.id           
          let item = 
            div [clazz "item"][
              i [clazz "large map pin middle aligned icon"] []
              div [clazz "content"] [
                a [clazz "header"][text "Feature"]
                div [clazz "description"] [text id]

              ]            
            ]
          yield item 

        }

      require (semui)(
        body [] [
          Incremental.div ([clazz "ui relaxed divided list"] |> AttributeMap.ofList) content
        ]
      )
      
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
  