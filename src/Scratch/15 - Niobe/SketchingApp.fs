namespace Niobe.Sketching

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Primitives

open Niobe

module Sg = 
  let v3f (input:V3d) : V3f = input |> V3f

  let pointsGetHead points =    
      points 
        |> AList.toMod 
        |> Mod.map(fun x -> (PList.tryAt 0 x) |> Option.defaultValue V3d.Zero)

  let toColoredEdges (offset:V3d) (color : C4b) (points : alist<V3d>) =
      points
        //|> AList.map (fun x -> x-offset)
        |> AList.toMod
        |> Mod.map(fun x-> 
          x |> PList.toList 
            |> List.pairwise 
            |> List.map(fun (a,b) -> (new Line3d(a,b), color)) |> PList.ofList)
        |> AList.ofMod
        //|> Array.pairwise
        //|> Array.map (fun (a,b) -> (new Line3d(a,b), color))

  let drawColoredEdges (width:IMod<float>) edges = 
    edges
      |> IndexedGeometryPrimitives.lines
      |> Sg.ofIndexedGeometry
      |> Sg.effect [
        toEffect DefaultSurfaces.trafo
        toEffect DefaultSurfaces.vertexColor
        toEffect DefaultSurfaces.thickLine
      ]
      |> Sg.uniform "LineWidth" width

  let viewLines points (color : IMod<C4b>) (width : IMod<float>) =           
    let head = pointsGetHead points
    adaptive {
      let! c = color
      let! h = head
      let! edges = toColoredEdges h c points |> AList.toMod
      return edges |> PList.toList |> drawColoredEdges width
    } |> Sg.dynamic
    
  let getPointsAndColors points (color : IMod<C4b>) = 
    let head = points |> pointsGetHead
      
    let pointsF = 
      points 
        |> AList.toMod 
        |> Mod.map2(
          fun h points -> 
            points |> PList.map(fun (x:V3d) -> (x-h) |> v3f) |> PList.toArray) head
       
    let colors = color |> Mod.map2(fun count c -> List.init count (fun _ -> c.ToC4f()) |> List.toArray ) (points |> AList.count)

    pointsF, colors, head

  let viewColoredPoints points (color : IMod<C4b>)= 
    let pointsF, colors, head = getPointsAndColors points color
    Sg.draw IndexedGeometryMode.PointList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF      
      |> Sg.vertexAttribute DefaultSemantic.Colors colors      
      |> Sg.effect [
         DefaultSurfaces.trafo |> toEffect
         DefaultSurfaces.vertexColor |> toEffect
         Shader.PointSprite.Effect
      ]
      |> Sg.translate' head
      |> Sg.uniform "PointSize" (Mod.constant 10.0)

  let generatePolygonTriangles (color : C4b) (offset : float) (points:alist<V3d>) =
    points 
     |> AList.toMod
     |> Mod.map(fun x -> 
        let plane = Plane3d()               // todo... generate plane from points
        let extrudeNormal = plane.Normal
        let projectedPointsOnPlane = x |> PList.map(fun p -> plane.Project p)
        //(projectedPointsOnPlane, extrudeNormal) // TODO!!
        (x, V3d.ZAxis)
        )
     |> Mod.map(fun (points, extrusionDir) -> 
       let list = points |> PList.toList
       
       // Top and Bottom triangle-fan startPoint
       let startPoint = list |> List.head
       let startPos = startPoint + extrusionDir * offset
       let startNeg = startPoint - extrusionDir * offset
       
       list
        |> List.pairwise    // Tuples for each edge
        |> List.mapi (fun i (a,b) -> 
           // Shift points 
           let aPos = a + extrusionDir * offset
           let bPos = b + extrusionDir * offset
           let aNeg = a - extrusionDir * offset
           let bNeg = b - extrusionDir * offset
           
           // Generate Triangles for watertight polygon
           [
               if i <> 0 then // first edge has to be skipped for top and bottom triangle generation
                   yield Triangle3d(startPos, bPos, aPos), C4b.DarkBlue  // top
                   yield Triangle3d(startNeg, aNeg, bNeg), C4b.DarkGreen // bottom

               yield Triangle3d(aPos, bNeg, aNeg), color // side1
               yield Triangle3d(aPos, bPos, bNeg), color // side2
           ] |> List.toSeq
           ) |> PList.ofList
        )

  let drawColoredPolygon sides =
    sides 
      |> Seq.map (fun x -> x |> IndexedGeometryPrimitives.triangles |> Sg.ofIndexedGeometry)
      |> Sg.ofSeq
      |> Sg.effect [
        toEffect DefaultSurfaces.trafo
        toEffect DefaultSurfaces.vertexColor
      ]

  let viewPolygon points (color :IMod<C4b>) =
    adaptive {
        let! c = color
        let! sides = generatePolygonTriangles c 2.0 points
        return sides |> drawColoredPolygon
    } |> Sg.dynamic

module SketchingApp =   
    
  let emptyBrush = 
    {
      points = PList.empty
      color = C4b.VRVisGreen
    }

  let update (model : SketchingModel) (action : SketchingAction) : SketchingModel =
    match action with
    | ClosePolygon -> 
      let b' =
        match model.working with
        | Some b -> Some { b with points = b.points |> PList.prepend (b.points |> PList.last) }  // add starting point to end
        | None -> None
      { model with working = b'; past = Some model }
    | AddPoint p ->
      let b' =
        match model.working with
        | Some b -> Some { b with points = b.points |> PList.prepend p }
        | None -> Some { emptyBrush with points = p |> PList.single }
      { model with working = b'; past = Some model }
    | SetThickness a ->
      { model with selectedThickness = Numeric.update model.selectedThickness a }
    | Undo _ -> 
      match model.past with
        | None -> model // if we have no past our history is empty, so just return our current model
        | Some p -> { p with future = Some model }
    | Redo _ -> 
      match model.future with
        | None -> model
        | Some f -> f
    | ChangeColor a ->
      let c = ColorPicker.update model.selectedColor a
      let b' = model.working |> Option.map(fun b -> { b with color = c.c})
      { model with working = b'; selectedColor = c }
  
  let viewSg (model : MSketchingModel) : ISg<SketchingAction> = 
    let brush = 
      model.working 
      |> Mod.map(function
        | Some brush -> 
          [ 
            Sg.viewColoredPoints brush.points brush.color
            Sg.viewLines brush.points brush.color model.selectedThickness.value
          ] |> Sg.ofSeq
        | None -> Sg.empty
        )

    brush |> Sg.dynamic

  let areaSg (model : MSketchingModel) : ISg<SketchingAction> = 
    let brush = 
      model.working 
      |> Mod.map(function
        | Some brush ->
          brush.points
          |> AList.count 
          |> Mod.map(fun x -> 
             if x > 2 then
               Sg.viewPolygon brush.points brush.color           
             else Sg.empty
          ) |> Sg.dynamic
        | None -> Sg.empty
        )
    brush |> Sg.dynamic

  let dependencies = 
    Html.semui @ [        
      { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
      { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
    ] 

  let viewGui (model : MSketchingModel) =
    require dependencies (
      Html.SemUi.accordion "Brush" "paint brush" true [          
          Html.table [  
              Html.row "Color:"  [ColorPicker.view model.selectedColor |> UI.map ChangeColor ]
              Html.row "Width:"  [Numeric.view model.selectedThickness |> UI.map SetThickness]                             
          ]          
      ]
    )