namespace Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Trafos

type ObjectType = 
    | Sphere of V3d * float 
    | Box of Box3d

[<ModelType>]
type Object =
    {
        [<PrimaryKey>]
        name            : string
        objectType      : ObjectType
        transformation  : Transformation
    }

[<ModelType>]
type World =
    {
        objects         : HashMap<string, Object>
        selectedObjects : HashSet<string>
    }


[<ModelType>]
type Scene =
    {
        world  : World
        kind   : TrafoKind
        mode   : TrafoMode
        camera : CameraControllerState
    }