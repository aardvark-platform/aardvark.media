namespace OpcSelectionViewer.Picking

open Aardvark.UI
open Aardvark.Base
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
      { model with intersectionPoints = points |> PList.ofList }
    | ClearPoints -> 
      { model with intersectionPoints = PList.empty }
    //| _ -> model