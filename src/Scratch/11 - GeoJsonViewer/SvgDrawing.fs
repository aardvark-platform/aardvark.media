namespace GeoJsonViewer

open Aardvark.Base
open Aardvark.UI

module Svg = 

    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

    let v2dToString (v : V2d) =
      sprintf "%f,%f" v.X -v.Y

    let drawPoint attributes (coord : V2d) =

      Svg.circle <| attributes @ [
        "cx" ==> coord.X.ToString()
        "cy" ==> (-coord.Y).ToString()
        "r"  ==> (25.0f).ToString()
      ]
      
    let drawPolygon attributes (coords : list<V2d>) =
      let coordString : string = 
        coords |> List.fold(fun (a : string) b -> a + " " + (b |> v2dToString)) ""

      Svg.polygon <| attributes @ [
        "points" ==> coordString
      ]      

    let drawBox attributes (box : Box2d) =      
      Svg.rect <| attributes @ [
            "x" ==> sprintf "%f"      box.Min.X
            "y" ==> sprintf "%f"      (-box.Min.Y - box.SizeY)
            "width" ==> sprintf  "%f" box.SizeX
            "height" ==> sprintf "%f" box.SizeY            
        ]
        
    let normTrafo (bb:Box2d) (canvas:V2d) = 
      (Trafo2d.Translation -bb.Min) *
        (Trafo2d.Scale (canvas / bb.Size))

    let svgDrawBoundingBoxNorm attributes (globalBox : Box2d) (canvasSize : V2d) (box : Box2d) =
      let trafo = normTrafo globalBox canvasSize        

      let b = box.Transformed(trafo)
      b|> drawBox attributes

    let drawPolygonNorm attributes (globalBox : Box2d) (canvasSize : V2d) (coords : list<V2d>) =
      let trafo = normTrafo globalBox canvasSize
           
      coords |> List.map(trafo.Forward.TransformPos) |> drawPolygon attributes

    let drawPointNorm attributes (globalBox : Box2d) (canvasSize : V2d) (coords : list<V2d>) =
      let trafo = normTrafo globalBox canvasSize
           
      coords |> List.map(trafo.Forward.TransformPos) |> List.head |> drawPoint attributes

  

