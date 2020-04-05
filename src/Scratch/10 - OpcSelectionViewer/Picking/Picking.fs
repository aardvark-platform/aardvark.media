namespace OpcSelectionViewer.Picking

open Aardvark.UI
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open OpcSelectionViewer

module PickingApp =

  let update (model : PickingModel) (msg : PickingAction) = 
    match msg with
    | HitSurface (box, sceneHit) -> 
      IntersectionController.intersect model "" sceneHit box
    | RemoveLastPoint ->
      let points = 
        match model.intersectionPoints.AsList with
          | [] -> []
          | _ :: rest -> rest
      { model with intersectionPoints = points |> IndexList.ofList }
    | ClearPoints -> 
      { model with intersectionPoints = IndexList.empty }
    //| _ -> model

  let toV3f (input:V3d) : V3f= input |> V3f

  let drawColoredPoints (points : alist<V3d>) =
    
    let head = 
      points 
        |> AList.toAVal 
        |> AVal.map(fun x -> (IndexList.tryAt 0 x) |> Option.defaultValue V3d.Zero)
      
    let pointsF = 
      points 
        |> AList.toAVal 
        |> AVal.map2(
          fun h points -> 
            points |> IndexList.map(fun (x:V3d) -> (x-h) |> toV3f) |> IndexList.toArray
            ) head
       

    Sg.draw IndexedGeometryMode.PointList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF
      |> Sg.effect [
         toEffect DefaultSurfaces.stableTrafo
         toEffect (DefaultSurfaces.constantColor C4f.Red)
         Shader.PointSprite.Effect
      ]
      |> Sg.translate' head
      |> Sg.uniform "PointSize" (AVal.constant 10.0)

  let view (model : AdaptivePickingModel) =
    drawColoredPoints model.intersectionPoints