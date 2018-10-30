namespace OpcSelectionViewer.Picking

open Aardvark.UI
open Aardvark.Base
open OpcSelectionViewer

module PickingApp =
  open Aardvark.Base.Incremental
  open Aardvark.Base.Rendering

  let update (model : PickingModel) (msg : PickingAction) = 
    match msg with
    | HitSurface (box, sceneHit) -> 
      IntersectionController.intersect model "" sceneHit box
    | RemoveLastPoint ->
      let points = 
        match model.intersectionPoints.AsList with
          | [] -> []
          | _ :: rest -> rest
      { model with intersectionPoints = points |> PList.ofList }
    | ClearPoints -> 
      { model with intersectionPoints = PList.empty }
    //| _ -> model

  let drawColoredPoints (points : alist<V3d>) =
    let pointsF = 
      points 
        |> AList.map V3f
        |> AList.toMod 
        |> Mod.map PList.toArray

    Sg.draw IndexedGeometryMode.PointList
      |> Sg.vertexAttribute DefaultSemantic.Positions pointsF
      |> Sg.effect [
         toEffect Aardvark.UI.Trafos.Shader.stableTrafo
         toEffect (DefaultSurfaces.constantColor C4f.Red)
         Shader.PointSprite.Effect
      ]
      |> Sg.uniform "PointSize" (Mod.constant 10.0)

  let view (model : MPickingModel) =
    drawColoredPoints model.intersectionPoints