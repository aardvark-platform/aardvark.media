﻿namespace OpcSelectionViewer

open System
open System.IO
open Aardvark.UI
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Rendering.Text
open Aardvark.UI.Primitives
open Aardvark.SceneGraph.Opc
open FShade
open Aardvark.Base.Geometry
open Aardvark.Geometry




module App = 
  //let intersectController (ci : int) (model : Model) (s : VrState) = 
  //      let ray = Ray3d(V3d.Zero, V3d.OIO)
  //      let ray = ray.Transformed(s.devices.[ci].pose.deviceToWorld.Forward)

  //      let hits = 
  //          model.kdTrees |> List.choose (fun (tree, trafo : Trafo3d) ->
  //              let ray = ray.Transformed(trafo.Backward) |> FastRay3d

  //              let tryIntersect (r : RayPart) (tri : Triangle3d) =
  //                  let mutable t = 0.0
  //                  if tri.Intersects(r.Ray.Ray, &t) && t >= r.TMin - 0.0001 && t <= r.TMax + 0.0001 then
  //                      Some (RayHit(t,tri))
  //                  else
  //                      None

  //              KdTree.intersect tryIntersect (RayPart(ray, 0.0, 0.5)) tree

  //          )

  //      match hits with
  //          | [] ->
  //              None
  //          | hits ->
  //              let h = hits |> List.minBy (fun h -> h.T)
  //              let pt = ray.GetPointOnRay h.T
  //              Some pt

  
  //let intersectKdTrees' = KdTrees.intersectKdTrees computeIndices

  //let mutable cache = hmap.Empty
  //let intersectController' (ci : int) (model : Model) (s : VrState) = 
  //    let ray = Ray3d(V3d.Zero, V3d.OIO)
  //    let ray = ray.Transformed(s.devices.[ci].pose.deviceToWorld.Forward)

  //    let hitBoxes =
  //      model.kdTrees2
  //        |> HMap.toList 
  //        |> List.map fst
  //        |> List.choose(
  //            fun x -> 
  //               let mutable t = 0.0                 
  //               let ray = ray.Transformed(model.finalTransform.Backward)
  //               match x.Intersects(ray, &t) with
  //               | true -> Some x
  //               | false -> None
  //        )          

  //    Log.warn "[Program] found %A hits" hitBoxes.Length
      
  //    let hit =
  //      hitBoxes
  //        |> Seq.choose(
  //            fun bb ->                                                                
  //                let ray' = ray.Transformed(model.finalTransform.Backward) |> FastRay3d  //combine pre and current transform?            
  //                let hit, c = 
  //                   model.kdTrees2 
  //                     |> intersectKdTrees' bb 0 cache ray'
          
  //                cache <- c                    
  //                hit |> Option.map(fun (t,_) -> 
  //                  let hitpoitn = ray'.Ray.GetPointOnRay t
  //                  Log.line "[program] hit in kdtree space %A %A" t hitpoitn
  //                  t, hitpoitn)
  //                )                                
  //        |> Seq.sortBy(fun (t,_)-> t)
  //        |> Seq.tryHead                                               
      
  //    hit |> Option.map(fun (t,x) -> Log.error "%A" t; model.finalTransform.Forward.TransformPos(x)) // |> Option.map ray.GetPointOnRay        
                    
  // let cycleMode (mode : InteractionMode) : InteractionMode =
  //   match mode with
  //     | Direct   -> Tele
  //     | Tele     -> Poly
  //     | Poly     -> DnS
  //     | DnS      -> Reset
  //     | Reset    -> Direct
  ////     | Tele   -> Direct
  //     | _  -> mode

  let update (model : Model) (msg : Message) =   
    match msg with
      | Camera m -> 
        { model with cameraState = FreeFlyController.update model.cameraState m; }
      | Message.KeyDown m ->
        Report.Error "Intersection was hit"; model      
      | Message.KeyUp m ->
        Report.Error "Intersection was released"; model
      | HitSurface sceneHit -> 
        let fray = sceneHit.globalRay.Ray  
        
        

        //match model.measureMode with 
        //| Tele ->
        //  Log.line "[Program] received teleport message %A" hitPoint           
        //  if hitPoint.IsNaN then
        //    { model with teleportBeacon = None }            
        //  else
        //    let trafo = -hitPoint |> Trafo3d.Translation
        //    Log.line "[Program] compute target trafo %A" (trafo.Forward.TransformPos hitPoint)
            
        //    let newBeacon = Some V3d.Zero // model.teleportBeacon |> Option.map(trafo.Forward.TransformPos)
          
        //    { 
        //      model with
        //          teleportBeacon = newBeacon
        //          teleportTrafo  = None
        //          finalTransform = model.finalTransform * trafo //* Trafo3d.Translation(V3d.OOI * 0.4)
        //    }
        model
  
  let pickable' (pick :IMod<Pickable>) (sg: ISg) =
        Sg.PickableApplicator (pick, Mod.constant sg)
      
  let view (m : MModel) =
              
      //state.runtime.PrepareGlyphs(font, [0..127] |> Seq.map char)
      //state.runtime.PrepareTextShaders(font, state.signature)                                   
      
      let box = 
        m.patchHierarchies
          |> List.map(fun x -> x.tree |> QTree.getRoot) 
          |> List.map(fun x -> x.info.LocalBoundingBox)
          |> List.fold (fun a b -> Box3d.Union(a, b)) Box3d.Invalid
    
      let pickable = 
        adaptive {
          return { shape = PickShape.Box box; trafo = Trafo3d.Identity }
        }  

      let scene = 
        Sg.opcSg m m.finalTransform
          |> Sg.fillMode m.fillMode            
          |> Sg.shader {
              do! DefaultSurfaces.trafo
              do! DefaultSurfaces.diffuseTexture
              do! DefaultSurfaces.simpleLighting
          } 
          |> Sg.noEvents
          |> pickable' pickable
          |> Sg.noEvents
          |> Sg.withEvents [
              SceneEventKind.Down, (
                fun sceneHit -> 
                  true, Seq.ofList[HitSurface sceneHit]                  
              )
          ]
          
      
          
          //|> wrap
          //|> Semantic.renderObjects
      
      let renderControl =
       FreeFlyController.controlledControl m.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
         (AttributeMap.ofList [ 
           style "width: 100%; height:100%"; 
           attribute "showFPS" "true";       // optional, default is false
           attribute "useMapping" "true"
           attribute "data-renderalways" "true"
           attribute "data-samples" "8"
           onKeyDown (Message.KeyDown)
           onKeyUp (Message.KeyUp)
           //onKeyDown (KeyDown)
           //onKeyUp (KeyUp)
         ]) 
         (scene)


      let frustum = Frustum.perspective 60.0 0.01 50000.0 1.0 |> Mod.constant          
        
      let cam = Mod.map2 Camera.create m.cameraState.view frustum 


      require Html.semui ( // we use semantic ui for our gui. the require function loads semui stuff such as stylesheets and scripts
          div [clazz "ui"; style "background: #1B1C1E"] [
              yield 
                  renderControl]              
        )

  let app dir =
      Serialization.registry.RegisterFactory (fun _ -> KdTrees.level0KdTreePickler)

      let phDirs = Directory.GetDirectories(dir)

      //let box = 
      //  Box3d.Parse("[[-2486972.923809925, 2288926.293124544, -275794.479790366], [-2486972.675580427, 2288926.581096648, -275793.402162172]]") 

      let patchHierarchies =
        [ 
            //for h in phDirs do
                yield PatchHierarchy.load Serialization.binarySerializer.Pickle Serialization.binarySerializer.UnPickle (phDirs.[0] |> OpcPaths)
        ]    

      let box = 
        patchHierarchies 
          |> List.map(fun x -> x.tree |> QTree.getRoot) 
          |> List.map(fun x -> x.info.GlobalBoundingBox)
          |> List.fold (fun a b -> Box3d.Union(a, b)) Box3d.Invalid
      
          
      let kdTreesPerHierarchy =
        [|
          for h in patchHierarchies do                
            yield KdTrees.loadKdTrees' h Trafo3d.Identity true ViewerModality.XYZ Serialization.binarySerializer
        |]

      let opcInfos = 
        [
          for h in patchHierarchies do
            
            let rootTree = h.tree |> QTree.getRoot

            yield {
              patchHierarchy = h
              kdTree         = KdTrees.loadKdTrees' h Trafo3d.Identity true ViewerModality.XYZ Serialization.binarySerializer
              localBB        = rootTree.info.LocalBoundingBox 
              globalBB       = rootTree.info.GlobalBoundingBox
            }

        ]

      let totalKdTrees = kdTreesPerHierarchy.Length
      Log.line "creating %d kdTrees" totalKdTrees
      let kdTrees = 
        kdTreesPerHierarchy
          |> Array.Parallel.mapi (fun i e ->
            Log.start "creating kdtree #%d of %d" i totalKdTrees
            let r = e
            Log.stop()
            r
           )
          |> Array.fold (fun a b -> HMap.union a b) HMap.empty

      let centerTransform = 
        Trafo3d.Translation(-box.Center) *
        Trafo3d.RotateInto(box.Center.Normalized, V3d.OIO)
      
      let height = V3d.Distance(box.Center, box.Min)
      
      let initialTransform = centerTransform * Trafo3d.Translation(V3d.OOI * 0.4)

      let camState = { FreeFlyController.initial with view = CameraView.lookAt V3d.III V3d.OOO V3d.OOI }

      let initialModel : Model = 
        { 
          cameraState          = camState
          distance             = None
          line                 = None
          fillMode             = FillMode.Fill
          renderLine           = false 
          showRay              = None
          teleportBeacon       = None
          teleportTrafo        = None
          patchHierarchies     = patchHierarchies
          kdTrees              = List.empty // Opc.getLeafKdTrees Opc.mars.preTransform  patchHierarchies |> Array.toList
          kdTrees2             = kdTrees
          opcInfos             = opcInfos
          picked               = HMap.empty
          

          workingDns           = None
          
          lines                = List.empty
          threads              = FreeFlyController.threads camState |> ThreadPool.map Camera
          boxes                = [] //kdTrees |> HMap.toList |> List.map fst
          initialTransform     = initialTransform
          finalTransform       = initialTransform
        }


      {
          initial = initialModel
             
          update = update
          view   = view
          
          threads = fun m -> m.threads
          unpersist = Unpersist.instance<Model, MModel>
      }
        

//let update (model : Model) (msg : Message) =
//    match msg with
//        Inc -> { model with value = model.value + 1 }

//let view (model : MModel) =
//    div [] [
//        text "Hello World"
//        br []
//        button [onClick (fun _ -> Inc)] [text "Increment"]
//        text "    "
//        Incremental.text (model.value |> Mod.map string)
//        br []
//        img [
//            attribute "src" "https://upload.wikimedia.org/wikipedia/commons/6/67/SanWild17.jpg"; 
//            attribute "alt" "aardvark"
//            style "max-width: 80%; max-height: 80%"
//        ]
//    ]


//let threads (model : Model) = 
//    ThreadPool.empty


//let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
//    {
//        unpersist = Unpersist.instance     
//        threads = threads 
//        initial = 
//            { 
//               value = 0
//            }
//        update = update 
//        view = view
//    }
