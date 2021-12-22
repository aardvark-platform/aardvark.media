namespace GeoJsonViewer

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Events


module App =

  let update (model : Model) (msg : Message) : Model =
      match msg with
      | Select s -> { model with selected = Some s }
      | Camera m -> { model with camera = FreeFlyController.update model.camera m; }
      | Deselect -> model // { model with selected = None }
      | Message.KeyDown k -> model
      | Message.KeyUp k -> model
      | UpdateConfig cfg ->
          { model with docking = cfg; }

  let semui = 
    [ 
      { kind = Stylesheet; name = "semui"; url = "./rendering/semantic.css" }
      { kind = Stylesheet; name = "semui-overrides"; url = "./rendering/semantic-overrides.css" }
      { kind = Script;     name = "semui"; url = "./rendering/semantic.js" }
    ]
  
  let isSelected (model : AdaptiveModel) (feature : Feature) = 
    model.selected 
      |> AVal.map(function
        | Some x -> x = feature.id
        | None -> false)

  let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

  module StylesSvg = 
    let root = 
      [ 
        "fill" ==> "none"
        "stroke" ==> "#A8814C"
        "stroke-width" ==> "2"
        "stroke-opacity" ==> "0.1"
      ]

    let idle = 
      [ 
        "fill" ==> "blue"
        "stroke" ==> "darkblue"
        "stroke-width" ==> "1"
        "fill-opacity" ==> "0.1"
      ]

    let selected = 
      [ 
        "fill" ==> "blue"
        "stroke" ==> "darkblue"
        "stroke-width" ==> "4"
        "fill-opacity" ==> "0.5"
      ]

  let viewFeaturesGui (model:AdaptiveModel) =
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
           // Log.line "coloring %A blue" f.properties.id
            [clazz "ui large map pin inverted middle aligned icon"; style "color:blue"]
          else 
            [clazz "ui large map pin inverted middle aligned icon"]
        
        let attr : AttributeMap<Message> = attr |> AttributeMap.ofList
            
      //  let (FeatureId id) = f.properties.id
        let item =             
          div [onMouseEnter (fun _ -> f.id |> Select); onMouseLeave (fun _ -> Deselect); clazz "ui inverted item"] [
            i attr2 []
            div [clazz "ui content"] [
              Incremental.div (attr) (AList.single (text "Feature"))
              div [clazz "ui description"] [text (f.id.ToString())] //[text (id.Replace('_',' '))]
            ]            
          ]
        yield item 
    }

  let viewFeaturesSvg (model:AdaptiveModel) = 

    let canvasSize = V2d(600.0, 600.0)

    let svgDrawFeature (globalBox : Box2d) (scale : V2d) (isSelected : bool) (feature : Feature) =
      let style = if isSelected then StylesSvg.selected else StylesSvg.idle
      let attr =[onMouseEnter (fun _ -> feature.id |> Select); onMouseLeave (fun _ -> Deselect) ] @ style
      [ 
        //feature.boundingBox |> svgDrawBoundingBoxNorm attr globalBox scale
        //feature.geometry.coordinates |> svgDrawPolyNorm attr globalBox scale
        feature.geometry.coordinates |> Svg.drawPointNorm attr globalBox scale
      ]
    
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
          yield bb |> Svg.svgDrawBoundingBoxNorm StylesSvg.root bb canvasSize

          for f in model.data.features do
            let! isSelected = f |> isSelected model
            yield! (f |> svgDrawFeature bb canvasSize isSelected) |> AList.ofList
            //yield f.geometry.coordinates |> svgDrawPolygon svgPolyAttributes
        }

    Incremental.Svg.svg svgAttr content

  //let viewSg (model:MModel) = 

  let withZ z (v:V2d) : V3d = 
    V3d(v.X, v.Y, z)

  let drawPlane (color : C4b) (bounds : Box2d) = 
    let points = bounds.ComputeCorners() |> Array.map (fun x -> V3d(x.X, x.Y, 0.0)) |> Array.take 4
    
    IndexedGeometryPrimitives.quad (points.[0], color) (points.[1], color) (points.[3], color) (points.[2], color)  |> Sg.ofIndexedGeometry    

  let drawFeature (color : C4b) (point : V2d) =
    let height = 0.2
    IndexedGeometryPrimitives.solidCone (point |> withZ height) -V3d.OOI height 0.05 25 color |> Sg.ofIndexedGeometry

  let drawFeature' (color : C4b) (point : V2d) =
    let height = 0.2
    IndexedGeometryPrimitives.point ((point |> withZ height).ToV3f()) color |> Sg.ofIndexedGeometry

  let drawFeatures (fc : AdaptiveFeatureCollection) = 

    let plane = 
      adaptive {
        let! bb = fc.boundingBox
        return bb |> drawPlane C4b.VRVisGreen         
      } |> Sg.dynamic

    let features = 
      alist {
        for f in fc.features do
          yield drawFeature' C4b.Red f.geometry.coordinates.[0]
      } |> AList.toASet |> Sg.set

    plane 
      |> Sg.andAlso features    
      |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.simpleLighting
      }

  let view (model : AdaptiveModel) =

    //let box = Sg.box (AVal.constant C4b.VRVisGreen) (AVal.constant Box3d.Unit)

    let sg = drawFeatures model.data
              
    let renderControl =
      FreeFlyController.controlledControl model.camera Camera (Frustum.perspective 60.0 0.01 1000.0 1.0 |> AVal.constant) 
        (AttributeMap.ofList [ 
          style "width: 100%; height:100%"; 
          attribute "showFPS" "false";       // optional, default is false
          attribute "useMapping" "true"
          attribute "data-renderalways" "false"
          attribute "data-samples" "4"
          onKeyDown (Message.KeyDown)
          onKeyUp (Message.KeyUp)
          //onBlur (fun _ -> Camera FreeFlyController.Message.Blur)
        ]) 
        (sg)
                                                                                        
    page (fun request ->
      match Map.tryFind "page" request.queryParams with
        | Some "list" ->
            require (semui)(
              body [ style "width: 100%; height:100%; background: transparent; overflow-x: hidden; overflow-y: scroll"] [
                Incremental.div ([clazz "ui very compact stackable inverted relaxed divided list"] |> AttributeMap.ofList) (viewFeaturesGui model)
              ]
            )
        | Some "render" -> 
            body [ style "width: 100%; height:100%; background: #636363; overflow: hidden"] [renderControl]
        | Some "map" -> 
            body [ style "width: 100%; height:100%; background: #636363; overflow: hidden"] [viewFeaturesSvg model]
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
      name = "initial"
      boundingBox = Box2d.Invalid
      typus       = Typus.Feature
      features    = IndexList.empty
    }
      
  let camPosition (bb : Box2d) =
    bb.Max |> withZ 2.0

  let initialCamera data = { 
    FreeFlyController.initial with 
      view = CameraView.lookAt (camPosition data.boundingBox) (data.boundingBox.Center |> withZ 0.0) V3d.OOI; }

  let app data =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         
    {
        unpersist = Unpersist.instance     
        threads   = threads 
        initial   = 
          { 
            camera   = initialCamera data
            data     = data
            selected = None
            docking  =
              config {
                  content (
                      horizontal 10.0 [
                        stack 0.7 None [
                          {id = "render"; title = Some " 3D View "; weight = 0.6; deleteInvisible = None; isCloseable = None}
                          {id = "map"; title = Some " Map View "; weight = 0.6; deleteInvisible = None; isCloseable = None}                          
                        ]
                        element { id "list"; title "Features"; weight 0.350; isCloseable false }
                      ]
                  )
                  appName "GeoJSON"
                  useCachedConfig false
              }
          }
        update    = update 
        view      = view
    }
  