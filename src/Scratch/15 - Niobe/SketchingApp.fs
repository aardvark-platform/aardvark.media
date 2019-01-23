namespace Niobe.Sketching

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Primitives

open Niobe

open Uncodium

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
      |> Sg.trafo(head |> Mod.map(fun x -> Trafo3d.Translation x))
      |> Sg.uniform "PointSize" (Mod.constant 10.0)

  let planeFit (points:seq<V3d>) : Plane3d =
    let length = points |> Seq.length |> float

    let c = 
        let sum = points |> Seq.reduce (fun x y -> V3d.Add(x,y))
        sum / length

    let pDiffAvg = points |> Seq.map(fun x -> x - c)
    
    let mutable matrix = M33d.Zero
    pDiffAvg |> Seq.iter(fun x -> matrix.AddOuterProduct(&x))
    matrix <- matrix / length
     
    let mutable q = M33d.Zero
    let mutable w = V3d.Zero
    let passed = Eigensystems.Dsyevh3(&matrix, &q, &w)
    
    let n = 
        if w.X < w.Y then
            if w.X < w.Z then q.C0
            else q.C2
        else if w.Y < w.Z then q.C1
        else q.C2

    Plane3d(n, c)

  let projectedPointAndPlaneNormal points =
    points |> Mod.map(fun x -> 
      let plane = planeFit x
      let extrudeNormal = plane.Normal
      let projectedPointsOnPlane = x |> PList.map(plane.Project)
      projectedPointsOnPlane, extrudeNormal
     )

  let generatePolygonTriangles (color : C4b) (offset : float) (points:alist<V3d>) =
    points 
     |> AList.toMod
     |> projectedPointAndPlaneNormal
     |> Mod.map(fun (points, extrusionDir) -> 
       let list = points |> PList.toList
       
       // Top and Bottom triangle-fan startPoint
       let startPoint = list |> List.head
       let startPos = startPoint + extrusionDir * offset
       let startNeg = startPoint - extrusionDir * offset
       
       if list |> List.length < 3 then
         []
       else 
         list
           |> List.pairwise    // TODO PLIST.pairwise only reason to change type
           |> List.mapi (fun i (a,b) -> 
             // Shift points 
             let aPos = a + extrusionDir * offset
             let bPos = b + extrusionDir * offset
             let aNeg = a - extrusionDir * offset
             let bNeg = b - extrusionDir * offset
             
             // Generate Triangles for watertight polygon
             [
               if i <> 0 then // first edge has to be skipped for top and bottom triangle generation
                   yield Triangle3d(startPos, bPos, aPos), color // top
                   yield Triangle3d(startNeg, aNeg, bNeg), color // bottom
           
               yield Triangle3d(aPos, bNeg, aNeg), color // side1
               yield Triangle3d(aPos, bPos, bNeg), color // side2
             ]
           )|> List.concat
     )

  let drawColoredPolygon sides =
    sides 
     |> IndexedGeometryPrimitives.triangles 
     |> Sg.ofIndexedGeometry
     |> Sg.effect [
       toEffect DefaultSurfaces.trafo
       toEffect DefaultSurfaces.vertexColor
     ]

  let viewPolygon points (color:IMod<C4b>) (offset:IMod<float>) (alpha:IMod<float>) =
    adaptive {
        let! c = color
        let! o = offset
        let! a = alpha
        let colorAlpha = c.ToC4d() |> (fun x -> C4d(x.R, x.G, x.B, a).ToC4b())
        let! sides = generatePolygonTriangles colorAlpha o points
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
    | SetOffset a -> 
      { model with volumeOffset = Numeric.update model.volumeOffset a }
    | SetAlphaArea a -> 
      { model with alphaArea = Numeric.update model.alphaArea a }
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
        | Some brush -> Sg.viewPolygon brush.points brush.color model.volumeOffset.value model.alphaArea.value    
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
              Html.row "Offset:"  [Numeric.view model.volumeOffset |> UI.map SetOffset]                             
              Html.row "AlphaArea:" [Numeric.numericField (SetAlphaArea >> Seq.singleton) AttributeMap.empty model.alphaArea Slider]                            
          ]          
      ]
    )