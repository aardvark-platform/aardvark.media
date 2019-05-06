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
  open Niobe.Shader

  let toV3f (input:V3d) : V3f = input |> V3f

  let pointsGetHead points =    
      points 
        |> AList.toMod 
        |> Mod.map(fun x -> (PList.tryAt 0 x) |> Option.defaultValue V3d.Zero)
  
  let drawColoredPoints points (offset : IMod<float>) (color : IMod<C4b>)= 

    let head = pointsGetHead points
    
    let pointsF = 
      points 
      |> AList.toMod 
      |> Mod.map2(
        fun h points -> 
          points |> PList.map(fun x -> (x-h) |> toV3f) |> PList.toArray
          ) head

    let colors = color |> Mod.map2(fun count c -> List.init count (fun _ -> c.ToC4f()) |> List.toArray ) (points |> AList.count)
    
    Sg.draw IndexedGeometryMode.PointList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF      
      |> Sg.vertexAttribute DefaultSemantic.Colors colors      
      |> Sg.effect [
       //  PointSprite.specialTrafo    |> toEffect
         DefaultSurfaces.trafo |> toEffect
         Shader.PointSprite.Effect
         DefaultSurfaces.vertexColor |> toEffect
        // PointSprite.colorDepth |> toEffect
      ]
      |> Sg.trafo(head |> Mod.map(fun x -> Trafo3d.Translation x))
      |> Sg.uniform "PointSize" (Mod.constant 10.0)
      |> Sg.uniform "depthOffset" offset

  let drawColoredConnectionLines points (color : IMod<C4b>) (offset:IMod<float>) (width : IMod<float>) =           
    
    let toColoredEdges (color : C4b) (points : alist<V3d>) =
      points
        |> AList.toMod
        |> Mod.map(fun x-> 
          x |> PList.toList 
            |> List.pairwise 
            |> List.map(fun (a,b) -> (new Line3d(a,b), color)) |> PList.ofList)
        |> AList.ofMod

    let drawColoredEdges (offset:IMod<float>) (width:IMod<float>) edges = 
      edges
        |> IndexedGeometryPrimitives.lines
        |> Sg.ofIndexedGeometry
        |> Sg.effect [
          toEffect DefaultSurfaces.trafo
          //toEffect PointSprite.specialTrafo
          toEffect LineRendering.thickLine2
          toEffect DefaultSurfaces.vertexColor
          // toEffect PointSprite.colorDepth        
        ]
        |> Sg.uniform "LineWidth" width
        |> Sg.uniform "depthOffset" offset

    adaptive {
      let! c = color
      let! edges = toColoredEdges c points |> AList.toMod
      return edges |> PList.toList |> drawColoredEdges offset width
    } |> Sg.dynamic
    
  let drawColoredPolygon points (color:IMod<C4b>) (offset:IMod<float>) (alpha:IMod<float>) :ISg<'a> =
    
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

    let generatePolygonTriangles (color : C4b) (offset : float) (points:alist<V3d>) =
      points 
      |> AList.toMod
      |> Mod.map(fun x -> 
        let plane = planeFit x
        let extrudeNormal = plane.Normal
        let projPointsOnPlane = x |> PList.map(plane.Project) |> PList.toList

        // Top and Bottom triangle-fan startPoint
        let startPoint = projPointsOnPlane |> List.head
        let startPos = startPoint + extrudeNormal * offset
        let startNeg = startPoint - extrudeNormal * offset
         
        if projPointsOnPlane |> List.length < 3 then
          []
        else 
          projPointsOnPlane
            |> List.pairwise    // TODO PLIST.pairwise only reason to change type
            |> List.mapi (fun i (a,b) -> 
              // Shift points 
              let aPos = a + extrudeNormal * offset
              let bPos = b + extrudeNormal * offset
              let aNeg = a - extrudeNormal * offset
              let bNeg = b - extrudeNormal * offset
               
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

    adaptive {
        let! c = color
        let! o = offset
        let! a = alpha
        let colorAlpha = c.ToC4d() |> (fun x -> C4d(x.R, x.G, x.B, a).ToC4b())
        let! polygon = generatePolygonTriangles colorAlpha o points
        return polygon
          |> IndexedGeometryPrimitives.triangles 
          |> Sg.ofIndexedGeometry
          |> Sg.effect [
            toEffect DefaultSurfaces.trafo
            toEffect DefaultSurfaces.vertexColor
          ]
    } |> Sg.dynamic

module SketchingApp =   

  let update (model : SketchingModel) (action : SketchingAction) : SketchingModel =
    match action with
    | ClosePolygon -> 
      match model.working with
      | None -> model
      | Some b ->
        let finishedBrush = { b with points = b.points |> PList.prepend (b.points |> PList.last) }
        { model with working = None; past = Some model; finishedBrusheds = model.finishedBrusheds |> PList.append finishedBrush }
    | AddPoint p ->
      let b' =
        match model.working with
        | Some b -> Some { b with points = b.points |> PList.prepend p }
        | None -> Some { color = model.selectedColor.c; points = p |> PList.single }
      { model with working = b'; past = Some model }
    | SetThickness a ->
      { model with selectedThickness = Numeric.update model.selectedThickness a }
    | SetOffset a -> 
      { model with volumeOffset = Numeric.update model.volumeOffset a }
    | SetDepthOffset a -> 
      { model with depthOffset = Numeric.update model.depthOffset a }
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
            Sg.drawColoredPoints brush.points model.depthOffset.value brush.color
            Sg.drawColoredConnectionLines brush.points brush.color model.depthOffset.value model.selectedThickness.value
          ] |> Sg.ofSeq
        | None -> Sg.empty)

    brush |> Sg.dynamic

  let polygonSg (model : MSketchingModel) = 
    
    let brush = 
      model.working 
      |> Mod.map(function
        | Some brush -> Sg.drawColoredPolygon brush.points brush.color model.volumeOffset.value model.alphaArea.value    
        | None -> Sg.empty
        ) 
      |> Sg.dynamic

    let finishedB = 
      model.finishedBrusheds 
        |> AList.map(fun br -> 
            //x |> AList.map(fun br -> 
              Sg.drawColoredPolygon br.points br.color model.volumeOffset.value model.alphaArea.value)
              //asd |> RenderCommand.SceneGraph)
              
          //let renderCommand = RenderCommand.Ordered asd
          //Sg.execute(renderCommand)) 
        //|> Sg.dynamic

    [
      brush 
      finishedB |> AList.toASet |> Sg.set
    ] |> Sg.ofList

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
              Html.row "DOffset:"  [Numeric.view model.depthOffset |> UI.map SetDepthOffset]                 
              Html.row "AlphaArea:" [Numeric.numericField (SetAlphaArea >> Seq.singleton) AttributeMap.empty model.alphaArea Slider]                            
          ]          
      ]
    )