namespace PlaceTransformObjects

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open DragNDrop
open Aardvark.UI.Trafos

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
        kind   : TrafoKind
        mode   : TrafoMode
        camera : CameraControllerState
    }