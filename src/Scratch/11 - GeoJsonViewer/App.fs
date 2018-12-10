namespace GeoJsonViewer

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Events

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

module App =


  let update (model : Model) (msg : Message) : Model =
      match msg with
      | Select s -> 
        Log.line "selected %A" s; 
        { model with selected = Some s }
      | Deselect -> model // { model with selected = None }
      | UpdateConfig cfg ->
          { model with docking = cfg; }

  let semui = 
    [ 
      { kind = Stylesheet; name = "semui"; url = "./rendering/semantic.css" }
      { kind = Script;     name = "semui"; url = "./rendering/semantic.js" }
    ]
  
  let isSelected (model : MModel) (feature : Feature) = 
    model.selected 
      |> Mod.map(function
        | Some x -> x = feature.id
        | None -> false)

  let view (model : MModel) =
                 
    let content =
      alist {
        
        for f in model.data.features do

          let! isSelected = f |> isSelected model

          let attr = 
            if isSelected then
              [clazz "ui header small"; style "color:blue"]
            else 
              [clazz "ui header small"]

          let attr2 = 
            if isSelected then
              Log.line "coloring %A blue" f.properties.id
              [clazz "ui large map pin inverted middle aligned icon"; style "color:blue"]
            else 
              [clazz "ui large map pin inverted middle aligned icon"]
          
          let attr : AttributeMap<Message> = attr |> AttributeMap.ofList
              
        //  let (FeatureId id) = f.properties.id
          let item =             
            div [onMouseEnter (fun _ -> f.id |> Select); onMouseLeave (fun _ -> Deselect); clazz "ui inverted item"][
              i attr2 []
              div [clazz "ui content"] [
                Incremental.div (attr) (AList.single (text "Feature"))
                div [clazz "ui description"] [text (f.id.ToString())] //[text (id.Replace('_',' '))]
              ]            
            ]
          yield item 
      }

    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

    //<svg width="400" height="110">
    //  <rect width="300" height="100" style="fill:rgb(0,0,255);stroke-width:3;stroke:rgb(0,0,0)" />
    //</svg> 

   // <polygon stroke="black" fill="none" transform="translate(100,0)"
   //points="50,0 21,90 98,35 2,35 79,90"/>

    let v2dToString (v : V2d) =
      sprintf "%f,%f" v.X -v.Y

    let svgDrawPolygon attributes (coords : list<V2d>) =
      let coordString : string = 
        coords |> List.fold(fun (a : string) b -> a + " " + (b |> v2dToString)) ""

      Svg.polygon <| attributes @ [
        "points" ==> coordString
      ]      

    let svgDrawBoundingBox attributes (box : Box2d) =      
      Svg.rect <| attributes @ [
            "x" ==> sprintf "%f"      box.Min.X
            "y" ==> sprintf "%f"      (-box.Min.Y - box.SizeY)
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

    let svgBBStyleSelected = 
      [ 
        "fill" ==> "blue"
        "stroke" ==> "darkblue"
        "stroke-width" ==> "4"
        "fill-opacity" ==> "0.5"
      ]

    //let svgPolyAttributes = 
    //  [
    //    "stroke" ==> "black"
    //    "fill" ==> "none"
    //  ]

    //let unpackId (id: FeatureId) = 
    //  let(FeatureId s) = id
    //  s

    let svgDrawBoundingBoxNorm attributes (globalBox : Box2d) (canvasSize : V2d) (box : Box2d) =
      let trafo = 
        (Trafo2d.Translation -globalBox.Min) *
        (Trafo2d.Scale (canvasSize / globalBox.Size))

      let b = box.Transformed(trafo)
      b|> svgDrawBoundingBox attributes

    let svgDrawPolyNorm attributes (globalBox : Box2d) (canvasSize : V2d) (coords : list<V2d>) =
      let trafo = 
        (Trafo2d.Translation -globalBox.Min) *
        (Trafo2d.Scale (canvasSize / globalBox.Size)) 

      let coords1 = coords |> List.map(trafo.Forward.TransformPos)
      Log.line "%A" coords1
      
      coords |> List.map(trafo.Forward.TransformPos) |> svgDrawPolygon attributes
      
    let svgDrawFeature (globalBox : Box2d) (scale : V2d) (isSelected : bool) (feature : Feature) =
      let style = if isSelected then svgBBStyleSelected else svgBBStyle
      let attr =[onMouseEnter (fun _ -> feature.id |> Select); onMouseLeave (fun _ -> Deselect) ] @ style
      [ 
        //feature.boundingBox |> svgDrawBoundingBoxNorm attr globalBox scale
        feature.geometry.coordinates |> svgDrawPolyNorm attr globalBox scale
      ]
                
    let svg = 
      
      let canvasSize = V2d(1280.0, 800.0)
      
      let svgAttr = 
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
            let! isSelected = f |> isSelected model
            yield! (f |> svgDrawFeature bb canvasSize isSelected) |> AList.ofList
            //yield f.geometry.coordinates |> svgDrawPolygon svgPolyAttributes
        }

      Incremental.Svg.svg svgAttr content

    page (fun request ->
      match Map.tryFind "page" request.queryParams with
        | Some "list" ->
            require (semui)(
              body [ style "width: 100%; height:100%; background: transparent; overflow-x: hidden; overflow-y: scroll"] [
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
            selected = None
            docking =
              config {
                  content (
                      horizontal 10.0 [
                          element { id "map";  title "2D Overview"; weight 0.649; isCloseable false }
                          element { id "list"; title "Features";    weight 0.350; isCloseable false }
                      ]
                  )
                  appName "GeoJSON"
                  useCachedConfig false
              }
          }
        update    = update 
        view      = view
    }
  