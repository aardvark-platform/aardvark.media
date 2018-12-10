namespace OpcSelectionViewer

open Aardvark.Base
open Aardvark.Base.Geometry   
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Opc

module Opc =
  open System.IO
  
  let createFlatISg (patchHierarchies : list<PatchHierarchy>) : ISg =
    let leaves = 
      patchHierarchies 
      |> List.collect(fun x ->  
        x.tree |> QTree.getLeaves |> Seq.toList |> List.map(fun y -> (x.opcPaths.Opc_DirAbsPath, y)))
    
    let sg = 
      let config = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }
    
      leaves 
        |> List.map(fun (dir,patch) -> (Patch.load (OpcPaths dir) ViewerModality.XYZ patch.info, dir, patch.info)) 
        |> List.map(fun ((a,_),c,d) -> (a,c,d))
        |> List.map (fun (g,dir,info) -> 
        
          let texPath = Patch.extractTexturePath (OpcPaths dir) info 0
          let tex = FileTexture(texPath,config) :> ITexture
        
          Sg.ofIndexedGeometry g
              |> Sg.trafo (Mod.constant info.Local2Global)             
              |> Sg.diffuseTexture (Mod.constant tex)             
          )
        |> Sg.ofList        
    sg

  type IUniformProvider with
    member x.TryGetViewTrafo() =
      match x.TryGetUniform(Ag.emptyScope, Symbol.Create "ViewTrafo") with
        | Some (:? IMod<Trafo3d> as t) -> t |> Some
        | Some (:? IMod<Trafo3d[]> as t) -> t |> Mod.map (Array.item 0) |> Some
        | _ -> None
  
      member x.TryGetProjTrafo() =
        match x.TryGetUniform(Ag.emptyScope, Symbol.Create "ProjTrafo") with
          | Some (:? IMod<Trafo3d> as t) -> t |> Some
          | Some (:? IMod<Trafo3d[]> as t) -> t |> Mod.map (Array.item 0) |> Some
          | _ -> None
  
  let isNan (v : V3f) =
    System.Single.IsNaN(v.X) || System.Single.IsNaN(v.Y) || System.Single.IsNaN(v.Z)

  let createOrLoadKdtree (dir : string) (p: Patch) (preTransform : Trafo3d) : KdTree<Triangle3d> * Trafo3d = 
    let opcPaths = dir |> OpcPaths 
    let g,_ = Patch.load opcPaths ViewerModality.XYZ p.info
    
    let trafo = p.info.Local2Global * preTransform
    
    let kdFile =  Path.combine [dir; "Patches"; p.info.Name; "kdtree.bin"]
    if File.Exists kdFile then
      Log.line "cache hit: %A" p.info.Name
    
      let tree : KdTree<Triangle3d> = Serialization.binarySerializer.UnPickle (File.ReadAllBytes kdFile)
    
      (tree, trafo)
    else
      let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
      let index = g.IndexArray |> unbox<int[]>
    
      let triangles =
        [| 0 .. 3 .. index.Length - 2 |] 
          |> Array.choose (fun bi -> 
            let p0 = pos.[index.[bi]]
            let p1 = pos.[index.[bi + 1]]
            let p2 = pos.[index.[bi + 2]]
            if isNan p0 || isNan p1 || isNan p2 then
                None
            else
                Triangle3d(V3d p0, V3d p1, V3d p2) |> Some
          )
                              
      Log.startTimed "build: %A" p.info.Name
      let tree = KdTree.build Spatial.triangle (KdBuildInfo(50, 5)) triangles
      Log.stop()
    
      let data = Serialization.binarySerializer.Pickle tree
      File.WriteAllBytes(kdFile, data)
    
      (tree, trafo)

  let getLeafKdTrees (preTransform : Trafo3d) (patchHierarchies : list<PatchHierarchy>) =
            
    let rec traverse (dir : string) (node : QTree<Patch>) =
      match node with
        | QTree.Leaf p ->
          [| createOrLoadKdtree dir p preTransform |]    
        | QTree.Node (_,c) ->
            c |> Array.collect (traverse dir)
    
    patchHierarchies |> List.toArray |> Array.collect (fun h -> traverse h.opcPaths.Opc_DirAbsPath h.tree) 

  let getRootKdTrees (preTransform : Trafo3d) (patchHierarchies : list<PatchHierarchy>) =

    let traverse (dir : string) (node : QTree<Patch>) =
      match node with 
        | QTree.Leaf p | QTree.Node (p,_) ->
          [| createOrLoadKdtree dir p preTransform |]    
                    
    patchHierarchies |> List.toArray |> Array.collect (fun h -> traverse h.opcPaths.Opc_DirAbsPath h.tree) 