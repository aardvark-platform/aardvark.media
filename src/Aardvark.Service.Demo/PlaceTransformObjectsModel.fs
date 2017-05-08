namespace PlaceTransformObjects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
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

[<DomainType>]
type Scene =
    {
        world  : World

        camera : CameraControllerState
    }