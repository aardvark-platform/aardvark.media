namespace OpcSelectionViewer

open Aardvark.Geometry
open Aardvark.Base


module KdTrees = 
  type LazyKdTree = {
      kdTree          : option<ConcreteKdIntersectionTree>
      affine          : Trafo3d
      boundingBox     : Box3d        
      kdtreePath      : string
      objectSetPath   : string
      coordinatesPath : string
      texturePath     : string
    }
  
  type InCoreKdTree = {
      kdTree      : ConcreteKdIntersectionTree
      boundingBox : Box3d
    }
  
  type Level0KdTree = 
      | LazyKdTree   of LazyKdTree
      | InCoreKdTree of InCoreKdTree