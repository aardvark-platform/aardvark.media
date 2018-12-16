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
  let v3f (input:V3d) : V3f= input |> V3f

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

  let drawColoredEdges width edges = 
    edges
      |> IndexedGeometryPrimitives.lines
      |> Sg.ofIndexedGeometry
      |> Sg.effect [
        toEffect DefaultSurfaces.trafo
        toEffect DefaultSurfaces.vertexColor
        toEffect DefaultSurfaces.thickLine
      ]
      |> Sg.uniform "LineWidth" (Mod.constant width)

  let viewLines points (color : IMod<C4b>) =           
    let head = pointsGetHead points
    adaptive {
      let! c = color
      let! h = head
      let! edges = toColoredEdges h c points |> AList.toMod
      return edges |> PList.toList |> drawColoredEdges 2.0
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

module SketchingApp =   
    
  let emptyBrush = 
    {
      points = PList.empty
      color = C4b.VRVisGreen
    }

  let update (model : SketchingModel) (action : SketchingAction) : SketchingModel =
    match action with
    | AddPoint p ->
      let b' =
        match model.working with
        | Some b -> Some { b with points = b.points |> PList.prepend p }
        | None -> Some { emptyBrush with points = p |> PList.single }
      { model with working = b' }
    | Undo -> model
    | Redo -> model
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
            Sg.viewLines brush.points brush.color
          ] |> Sg.ofSeq
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
              Html.row "Width:"  [text "Width"]                  
              Html.row "Width:"  [text "Width"]
          ]          
      ]
    )