namespace OpcSelectionViewer.Picking

module IntersectionController = 
  open Aardvark.Application
  open Aardvark.Base
  open Aardvark.UI
  open OpcSelectionViewer
  open KdTrees
  open Aardvark.Geometry
  open System
  open Aardvark.SceneGraph.Opc

  let hitBoxes (kd : hmap<Box3d, Level0KdTree>) (r : FastRay3d) (trafo : Trafo3d) =
    kd
      |> HMap.toList 
      |> List.map fst
      |> List.filter(
        fun x -> 
          let mutable t = 0.0
          let r' = r.Ray.Transformed(trafo.Backward) //combine pre and current transform
          x.Intersects(r', &t)
      )
  
  let loadTriangles (kd : LazyKdTree) = 
      loadTrianglesFromFile kd.objectSetPath kd.affine.Forward
      |> TriangleSet

  let loadObjectSet (cache : hmap<string, ConcreteKdIntersectionTree>) (lvl0Tree : Level0KdTree) =           
    match lvl0Tree with
      | InCoreKdTree kd -> 
        kd.kdTree, cache
      | LazyKdTree kd ->         
        let kdTree, cache =
          match kd.kdTree with
            | Some k -> k, cache
            | None -> 
              let tree = cache |> HMap.tryFind (kd.boundingBox.ToString())
              match tree with
                | Some t -> 
                  //Log.line "cache hit %A" kd.boundingBox
                  t, cache
                | None ->                                     
                  Log.line "cache miss %A- loading kdtree" kd.boundingBox

                  let mutable tree = loadKdtree kd.kdtreePath                                    
                  tree.KdIntersectionTree.ObjectSet <- (kd |> loadTriangles)

                  let key = tree.KdIntersectionTree.BoundingBox3d.ToString()
                                                      
                  tree, (HMap.add key tree cache)
        kdTree, cache

  let intersectSingle ray (hitObject : 'a) (kdTree:ConcreteKdIntersectionTree) = 
    let kdi = kdTree.KdIntersectionTree 
    let mutable hit = ObjectRayHit.MaxRange
    let objFilter _ _ = true              
    try           
      if kdi.Intersect(ray, Func<_,_,_>(objFilter), null, 0.0, Double.MaxValue, &hit) then              
          Some (hit.RayHit.T, hitObject)
      else            
          None
    with 
      | _ -> 
        Log.error "null ref exception in kdtree intersection" 
        None 
 
  let intersectSingleWithCoords ray (hitObject : 'a) (kdTree:ConcreteKdIntersectionTree) = 
    let kdi = kdTree.KdIntersectionTree 
    let mutable hit = ObjectRayHit.MaxRange
    let objFilter _ _ = true              
    try           
      if kdi.Intersect(ray, Func<_,_,_>(objFilter), null, 0.0, Double.MaxValue, &hit) then              
          Some (hit.RayHit.T, hit.RayHit.Coord)
      else            
          None
    with 
      | _ -> 
        Log.error "null ref exception in kdtree intersection" 
        None 

  let intersectKdTrees bb (hitObject : 'a) (cache : hmap<string, ConcreteKdIntersectionTree>) (ray : FastRay3d) (kdTreeMap: hmap<Box3d, Level0KdTree>) = 
      let kdtree, c = kdTreeMap |> HMap.find bb |> loadObjectSet cache
      let hit = intersectSingle ray hitObject kdtree
      hit,c

  let intersectKdTreeswithTexCoords bb (hitObject : 'a) (cache : hmap<string, ConcreteKdIntersectionTree>) (ray : FastRay3d) (kdTreeMap: hmap<Box3d, Level0KdTree>) = 
      let kdtree, c = kdTreeMap |> HMap.find bb |> loadObjectSet cache
      let hit = intersectSingleWithCoords ray hitObject kdtree
      hit,c


  let mutable cache = HMap.empty

  let intersectWithOpc (kdTree0 : option<hmap<Box3d, Level0KdTree>>) (hitObject : 'a) ray =
    kdTree0 
      |> Option.bind(fun kd ->
          let boxes = hitBoxes kd ray Trafo3d.Identity
          
          let closest = 
            boxes 
              |> List.choose(
                  fun bb -> 
                    let treeHit,c = kd |> intersectKdTrees bb hitObject cache ray
                    cache <- c
                    treeHit)
              |> List.sortBy(fun (t,_)-> t)
              |> List.tryHead            
          closest
      ) 
  
  let intersect (m : PickingModel) opc (hit : SceneHit) (boxId : Box3d) = 
    let fray = hit.globalRay.Ray
            
    let opcdings = m.pickingInfos |> HMap.tryFind boxId
    match opcdings with
    | Some kk ->

      let closest = intersectWithOpc (Some kk.kdTree) opc fray
      
      match closest with
        | Some (t,_) -> 
          let hitpoint = fray.Ray.GetPointOnRay t
          Log.line "hit surface at %A" hitpoint            
          { m with intersectionPoints = m.intersectionPoints |> PList.prepend hitpoint  }            
        | None -> m      
    | None -> m
      
      
      
      

