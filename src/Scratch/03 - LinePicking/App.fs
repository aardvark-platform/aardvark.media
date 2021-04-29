﻿namespace LinePickingDemo


open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering

open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

open Inc.Model

module LineDrawing =

  let toColoredEdges (color : C4b) (points : array<V3d>) =
    points
      |> Array.pairwise
      |> Array.map (fun (a,b) -> (new Line3d(a,b), color))

  let drawColoredEdges edges = 
    edges
      |> IndexedGeometryPrimitives.lines
      |> Sg.ofIndexedGeometry
      |> Sg.effect [
        toEffect DefaultSurfaces.stableTrafo
        toEffect DefaultSurfaces.vertexColor
        toEffect DefaultSurfaces.thickLine
      ]
      |> Sg.uniform "LineWidth" (AVal.constant 5.0)

  let sphere color size pos =
    let trafo = 
      pos |> AVal.map(fun (x : V3d) -> Trafo3d.Translation x)
    
    Sg.sphere 3 (AVal.constant color) (AVal.constant size)
      |> Sg.noEvents
      |> Sg.trafo trafo
        
      |> Sg.effect [
        toEffect <| DefaultSurfaces.stableTrafo    
        toEffect <| DefaultSurfaces.vertexColor
      ]

  let cylinders positions = 
    positions |> Array.pairwise |> Array.map(fun (a,b) -> Line3d(a,b)) |> Array.map (fun x -> Cylinder3d(x, 0.02))

module App =
  

  let intersect (m:Model) (r : Ray3d) =          
    let mutable hit = RayHit3d.MaxRange
    let result =
      m.cylinders 
        |> Array.tryFind(fun x -> 
            r.Hits(x, 0.0, 100.0, &hit))

    result |> Option.map(fun x -> (x,hit))
    

  let update (model : Model) (msg : Action) =
      match msg with
        | FreeFlyAction a when not model.isShift ->
          { model with camera = FreeFlyController.update model.camera a }
        | PickPolygon hit ->
          let r = hit.globalRay.Ray.Ray
          match r |> intersect model with
          | Some (_,h) -> 
            let hitpoint = r.GetPointOnRay(h.T)
            { model with hitPoint = Some hitpoint }
          | None -> 
            Log.error "no hit"
            { model with hitPoint = None }
        | KeyDown k ->
          match k with
            | Aardvark.Application.Keys.LeftShift 
            | Aardvark.Application.Keys.RightShift -> { model with isShift = true }
            | _ -> model
        | KeyUp k ->
          match k with
            | Aardvark.Application.Keys.LeftShift 
            | Aardvark.Application.Keys.RightShift -> { model with isShift = false }
            | _ -> model
        | _ -> model
  
  let pickingGeometries =
    let boxGeometry = Box3d(-V3d.III, V3d.III)
    let corners = boxGeometry.ComputeCorners()

    corners |> LineDrawing.cylinders


  let pickable' (pick :aval<Pickable>) (sg: ISg) =
        Sg.PickableApplicator (pick, AVal.constant sg)

  let scene (model:AdaptiveModel) =
    let color = AVal.constant C4b.Blue
    let boxGeometry = Box3d(-V3d.III, V3d.III)
    let corners = boxGeometry.ComputeCorners()

    
    let pickable = { shape = PickShape.Box (boxGeometry.Scaled(V3d(1.1))); trafo = Trafo3d.Identity } |> AVal.constant

    let wireBox picking =
      corners
        |> LineDrawing.toColoredEdges C4b.VRVisGreen 
        |> LineDrawing.drawColoredEdges
        |> pickable' pickable
        |> Sg.noEvents    
        |> Sg.withEvents [
           SceneEventKind.Down, (
             fun sceneHit ->
               if (picking |> AVal.force) then
                 true, Seq.ofList[PickPolygon (sceneHit)]
               else
                 false, Seq.ofList[])
        ]
                
    let box = AVal.constant (boxGeometry)
  
    let b = 
      Sg.box color box                            
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
            }      
        |> Sg.noEvents

    let hitPoint =
      model.hitPoint 
      |> AVal.map(
        function
          | Some y -> LineDrawing.sphere C4b.Red 0.08 (y|>AVal.constant)
          | None -> Sg.empty) |> Sg.dynamic
      
    [wireBox model.isShift; hitPoint] |> Sg.ofList
  
  let view (model : AdaptiveModel) =
    let frustum =
      AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
    
    let renderControlAttributes = 
      FreeFlyController.extractAttributes model.camera FreeFlyAction |> AttributeMap.ofAMap
    
    require Html.semui ( 
      div [clazz "ui"; style "background: #1B1C1E"] [
        yield 
          Incremental.renderControl 
            (AVal.map2 Camera.create model.camera.view frustum) 
            (AttributeMap.unionMany [
                renderControlAttributes                   
                [
                    attribute "style" "width:100%; height: 100%; float: left"
                    //attribute "data-renderalways" "true"
                    //attribute "showFPS" "true"
                    onKeyDown (KeyDown)
                    onKeyUp (KeyUp)
                ] |> AttributeMap.ofList
            ]) (scene model)
      ])
  
  let threads (model : Model) = 
      FreeFlyController.threads model.camera |> ThreadPool.map FreeFlyAction
    
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
      unpersist = Unpersist.instance     
      threads = threads 
      initial =  
        { 
          camera    = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
          cylinders = pickingGeometries
          hitPoint  = None  
          isShift   = false
        }            
      update = update 
      view = view
    }
