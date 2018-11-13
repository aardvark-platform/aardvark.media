namespace GeoJsonViewer

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module App =


  let update (model : Model) (msg : Message) : Model =
      match msg with
      | Inc -> model
      | UpdateConfig cfg ->
          { model with docking = cfg; }

  let semui = 
    [ 
      { kind = Stylesheet; name = "semui"; url = "./rendering/semantic.css" }
      { kind = Script;     name = "semui"; url = "./rendering/semantic.js" }
    ]
  
  let view (model : MModel) =
                 
    let content =
      alist {
        for f in model.data.features do
          let (FeatureId id) = f.properties.id           
          let id = id.Replace('_',' ')
          let item = 
            div [clazz "ui inverted item"][
              i [clazz "ui large map pin inverted middle aligned icon"] []
              div [clazz "ui content"] [
                a [clazz "ui header small"][text "Feature"]
                div [clazz "ui description"] [text id]
              ]            
            ]
          yield item 
      }

    page (fun request ->
      match Map.tryFind "page" request.queryParams with
        | Some "list" ->
            require (semui)(
              body [ style "width: 100%; height:100%; background: transparent; overflow: hidden"] [
                Incremental.div ([clazz "ui very compact stackable inverted relaxed divided list"] |> AttributeMap.ofList) content
              ]
            )
        | Some other -> 
          let msg = sprintf "Unknown page: %A" other
          body [] [
              div [style "color: white; font-size: large; background-color: red; width: 100%; height: 100%"] [text msg]
          ] 
        | None -> 
          model.docking 
            |> docking [
              style "width:100%;height:100%;"
              onLayoutChanged UpdateConfig
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
        initial   = 
          { 
            data = GeoJSON.load @"..\..\..\data\eox.json" 
            docking =
              config {
                  content (
                      horizontal 10.0 [
                          element { id "map";  title "2D Overview"; weight 3; isCloseable false }
                          element { id "list"; title "Features";    weight 2; isCloseable false }
                      ]
                  )
                  appName "GeoJSON"
                  useCachedConfig false
              }
          }
        update    = update 
        view      = view
    }
  