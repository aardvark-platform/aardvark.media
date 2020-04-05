namespace Niobe.Sketching

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.Application
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify.FSharp.Core
open Uncodium

module Sg = 

  let drawWorkingBrush (points:alist<V3d>) (color : aval<C4b>) (offset:aval<float>) (width : aval<float>) =           
    
    let pointsF =
      points 
      |> AList.toAVal 
      |> AVal.map(fun x -> x |> IndexList.toSeq |> Seq.map(fun y -> V3f y) |> Seq.toArray)

    let indexArray = 
      pointsF |> AVal.map(fun x -> (Array.init (max 0 (x.Length * 2 - 1)) (fun a -> (a + 1)/ 2)))

    let lines = 
      Sg.draw IndexedGeometryMode.LineList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF 
      |> Sg.index indexArray
      |> Sg.uniform "Color" color
      |> Sg.uniform "LineWidth" width
      |> Sg.uniform "depthOffset" offset
      |> Sg.effect [
        toEffect DefaultSurfaces.trafo
        //Shader.PointSprite.EffectCameraShift  // shifts the whole geometry into camera direction (hacky)
        toEffect DefaultSurfaces.thickLine
        toEffect DefaultSurfaces.sgColor
        ]

    let points = 
      Sg.draw IndexedGeometryMode.PointList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF 
      |> Sg.uniform "Color" (AVal.constant(C4b.Red))
      |> Sg.uniform "PointSize" (AVal.constant 10.0)
      |> Sg.uniform "depthOffset" offset
      |> Sg.effect [
        toEffect DefaultSurfaces.trafo
        //Shader.PointSprite.EffectCameraShift // shifts the whole geometry into camera direction (hacky)
        toEffect DefaultSurfaces.pointSprite // quad-like
        //Shader.PointSprite.EffectSprite // circle-like
        toEffect DefaultSurfaces.sgColor
        ]

    [ lines; points] 
      |> Sg.ofSeq
      |> Sg.depthBias (offset |> AVal.map (fun x -> DepthBiasState(x, 0.0, 0.0)))  // using this methode the bias depends on the near-far-plane ratio
    
  let drawFinishedBrush points (color:aval<C4b>) (alpha:aval<float>) (offset:aval<float>) :ISg<'a> =
    
    let planeFit (points:seq<V3d>) : Plane3d =
      let length = points |> Seq.length |> float

      let c = 
          let sum = points |> Seq.reduce (fun x y -> V3d.Add(x,y))
          sum / length

      let pDiffAvg = points |> Seq.map(fun x -> x - c)
      
      let mutable matrix = M33d.Zero
      pDiffAvg |> Seq.iter(fun x -> 
        let mutable x = x
        matrix.AddOuterProduct(&x)
      )
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
      |> AList.toAVal
      |> AVal.map(fun x -> 
        let plane = planeFit x
        let extrudeNormal = plane.Normal
        let projPointsOnPlane = x |> IndexList.map(plane.Project) |> IndexList.toList

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
          ) |> List.concat
       )

    let colorAlpha = AVal.map2(fun (c:C4b) a  -> 
      let col = c.ToC4d() 
      C4d(col.R, col.G, col.B, a).ToC4b()) color alpha

    adaptive {
        let! colorAlpha = colorAlpha
        let! o = offset
        let! polygon = generatePolygonTriangles colorAlpha o points
        
        return 
          polygon
            |> IndexedGeometryPrimitives.triangles 
            |> Sg.ofIndexedGeometry
            |> Sg.effect [
              toEffect DefaultSurfaces.trafo
              toEffect DefaultSurfaces.vertexColor
            ]

    } |> Sg.dynamic

module SketchingApp =   

  let emptyBrush = 
    {
      points = IndexList.empty
      color = C4b.VRVisGreen
    }

  let update (model : SketchingModel) (action : SketchingAction) : SketchingModel =
    match action with
    | ClosePolygon -> 
      match model.working with
      | None -> model
      | Some b ->
        let finishedBrush = { b with points = b.points |> IndexList.prepend (b.points |> IndexList.last) }
        { model with working = None; past = Some model; finishedBrushes = model.finishedBrushes |> IndexList.prepend finishedBrush }
    | AddPoint p ->
      let b' =
        match model.working with
        | Some b -> Some { b with points = b.points |> IndexList.prepend p }
        | None -> Some { emptyBrush with points = p |> IndexList.single }
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
  
  let currentBrushSg (model : AdaptiveSketchingModel) : ISg<SketchingAction> = 

    let color = model.working |> AVal.bind (fun x -> match x with | AdaptiveNone -> AVal.constant(C4b.Black) | AdaptiveSome a -> a.color)
    let points = model.working |> AList.bind (fun x -> match x with | AdaptiveNone -> AList.empty | AdaptiveSome a -> a.points)

    Sg.drawWorkingBrush points color model.depthOffset.value model.selectedThickness.value

  let finishedBrushSg (model : AdaptiveSketchingModel) = 

    model.finishedBrushes 
      |> AList.map(fun br -> Sg.drawFinishedBrush br.points br.color model.alphaArea.value model.volumeOffset.value )
      |> AList.toASet 
      |> Sg.set
        //// try renderCommand for draw order (vulkan)
        //RenderCommand.SceneGraph asd
        //Sg.execute(RenderCommand.Ordered asd)

  let dependencies = 
    Html.semui @ [        
      { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
      { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
    ] 

  let viewGui (model : AdaptiveSketchingModel) =
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