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

    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

    //<svg width="400" height="110">
    //  <rect width="300" height="100" style="fill:rgb(0,0,255);stroke-width:3;stroke:rgb(0,0,0)" />
    //</svg> 

    let svgDrawBoundingBox attributes (canvasSize : V2d)(box : Box2d) =
      
      Svg.rect <| attributes @ [
            "x" ==> sprintf "%f"      box.Min.X
            "y" ==> sprintf "%f"      box.Min.Y
            "width" ==> sprintf  "%f" box.SizeX
            "height" ==> sprintf "%f" box.SizeY
            
        ]
    
    let svgGlobalBBStyle = 
      [ 
        "fill" ==> "none"
        "stroke" ==> "#A8814C"
        "stroke-width" ==> "2"
        "stroke-opacity" ==> "0.1"
      ]

    let svgBBStyle = 
      [ 
        "fill" ==> "blue"
        "stroke" ==> "darkblue"
        "stroke-width" ==> "1"        
        "fill-opacity" ==> "0.1"
      ]

    let svgDrawBoundingBoxNorm attributes (globalBox : Box2d) (canvasSize : V2d) (box : Box2d) =
      let trafo = 
        (Trafo2d.Translation -globalBox.Min) *
        (Trafo2d.Scale (canvasSize / globalBox.Size))
      let b = box.Transformed(trafo) 
      b|> svgDrawBoundingBox attributes canvasSize

    let svgDrawFeature (globalBox : Box2d) (scale : V2d) (feature : Feature) =     
      feature.boundingBox |> svgDrawBoundingBoxNorm svgBBStyle globalBox scale

    let svg = 
      
      let canvasSize = V2d(640.0, 480.0)

      let blarg (bb : IMod<Box2d>)= 
        amap {
          //let! bb = bb
          yield "width" ==> sprintf "%f" canvasSize.X
          yield "height" ==> sprintf "%f" canvasSize.Y
          yield "viewBox" ==> sprintf ("%f %f %f %f") 0.0 (-canvasSize.Y) canvasSize.X canvasSize.Y
          yield clazz "svgRoot"
          yield style "border: 2px dashed black"

        } |>  AttributeMap.ofAMap
      
      let content =
        alist {
          let! bb = model.data.boundingBox
          yield bb |> svgDrawBoundingBoxNorm svgGlobalBBStyle bb canvasSize

          for f in model.data.features do
            yield f |> svgDrawFeature bb canvasSize
        }

      Incremental.Svg.svg (blarg model.data.boundingBox) content

    page (fun request ->
      match Map.tryFind "page" request.queryParams with
        | Some "list" ->
            require (semui)(
              body [ style "width: 100%; height:100%; background: transparent; overflow: hidden"] [
                Incremental.div ([clazz "ui very compact stackable inverted relaxed divided list"] |> AttributeMap.ofList) content
              ]
            )
        | Some "map" ->            
            body [ style "width: 100%; height:100%; background: #636363; overflow: hidden"] [svg]            
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
                          element { id "list"; title "Features";    weight 1; isCloseable false }
                      ]
                  )
                  appName "GeoJSON"
                  useCachedConfig false
              }
          }
        update    = update 
        view      = view
    }
  