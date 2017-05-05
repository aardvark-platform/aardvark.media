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
        selected        : bool
    }

[<DomainType>]
type World =
    {
        objects : hmap<string, Object>
    }

[<DomainType>]
type Scene =
    {
        world  : World

        selectedObject : Option<Object>
        camera         : CameraControllerState
    }