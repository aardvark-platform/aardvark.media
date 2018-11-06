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
    
  let loadTriangleSet (kd : LazyKdTree) =
    kd |> loadTriangles |> TriangleSet

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
                  tree.KdIntersectionTree.ObjectSet <- (kd |> loadTriangleSet)

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
 
  let intersectSingleForIndex ray (hitObject : 'a) (kdTree:ConcreteKdIntersectionTree) = 
      let kdi = kdTree.KdIntersectionTree 
      let mutable hit = ObjectRayHit.MaxRange
      let objFilter _ _ = true              
      try           
        if kdi.Intersect(ray, Func<_,_,_>(objFilter), null, 0.0, Double.MaxValue, &hit) then  
            Some (hit.RayHit.T, hit.SetObject.Index)
        else            
            None
      with 
        | _ -> 
          Log.error "null ref exception in kdtree intersection" 
          None 

  let calculateBarycentricCoordinates (triangle : Triangle3d) (pos : V3d) = 
    let v0 = triangle.P1 - triangle.P0
    let v1 = triangle.P2 - triangle.P0
    let v2 = pos         - triangle.P0

    let d00 = V3d.Dot(v0, v0)
    let d01 = V3d.Dot(v0, v1)
    let d11 = V3d.Dot(v1, v1)
    let d20 = V3d.Dot(v2, v0)
    let d21 = V3d.Dot(v2, v1)
    
    let denom = d00 * d11 - d01 * d01;

    let v = (d11 * d20 - d01 * d21) / denom
    let w = (d00 * d21 - d01 * d20) / denom
    let u = 1.0 - v - w
    
    V3d(v,w,u)

    
  let findCoordinates (kdTree : LazyKdTree) (index : int) (position : V3d) =
    let triangles = kdTree |> loadTriangles
    let triangle  = triangles.[index]
    
    let baryCentricCoords = calculateBarycentricCoordinates triangle position

    Log.line "barycentricCoords: u: %f, v: %f, w: %f" baryCentricCoords.X baryCentricCoords.Y baryCentricCoords.Z 

    baryCentricCoords

  let intersectKdTrees (bb : Box3d) (hitObject : 'a) (cache : hmap<string, ConcreteKdIntersectionTree>) (ray : FastRay3d) (kdTreeMap: hmap<Box3d, Level0KdTree>) = 
      let kdtree, c = kdTreeMap |> HMap.find bb |> loadObjectSet cache
      let hit = intersectSingle ray hitObject kdtree
      hit,c

  let intersectKdTreeswithObjectIndex (bb : Box3d) (hitObject : 'a) (cache : hmap<string, ConcreteKdIntersectionTree>) (ray : FastRay3d) (kdTreeMap: hmap<Box3d, Level0KdTree>) = 
      let kdtree, c =  kdTreeMap |> HMap.find bb |> loadObjectSet cache
      
      //let triangleSet = kdtree.KdIntersectionTree.
      let hit = intersectSingleForIndex ray hitObject kdtree
      
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
                    let treeHit,c = kd |> intersectKdTreeswithObjectIndex bb hitObject cache ray                    
                    cache <- c
                    match treeHit with 
                      | Some hit -> Some (hit,bb)
                      | None -> None)
              |> List.sortBy(fun (t,_)-> fst t)
              |> List.tryHead            
          
          match closest with
            | Some (values,bb) -> 
              let lvl0KdTree = kd |> HMap.find bb

              let position = ray.Ray.GetPointOnRay (fst values)

              let coordinates = 
                match lvl0KdTree with
                  | InCoreKdTree kd -> 
                    None
                  | LazyKdTree kd ->    
                    Some (findCoordinates kd (snd values) position)
                        
              Some values
            | None -> None

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
      
  