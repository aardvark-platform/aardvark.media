namespace PlaceTransformObjects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open DragNDrop

type ObjectType = 
    Sphere = 0 | Box = 1

[<DomainType>]
type Object =
    {
        [<PrimaryKey>]
        name            : string
        objectType      : ObjectType
        transformation  : Transformation
    }

[<DomainType>]
type World =
    {
        objects         : hmap<string, Object>
        selectedObjects : hset<string>
    }

type TrafoKind =
  | Translate = 0
  | Rotate    = 1
  | Scale     = 2

[<DomainType>]
type Scene =
    {
        world  : World
        kind   : TrafoKind
        mode   : TrafoMode
        camera : CameraControllerState
    }