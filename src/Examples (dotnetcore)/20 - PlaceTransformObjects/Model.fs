namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Trafos

type Action = 
    | PlaceBox 
    | Select        of string
    | SetKind       of TrafoKind
    | SetMode       of TrafoMode
    | Translate     of string * TrafoController.Action
    | Rotate        of string * TrafoController.Action
    | Scale         of string * TrafoController.Action
    | CameraMessage of CameraController.Message
    | UpdateConfig  of DockConfig
    | KeyDown       of key : Aardvark.Application.Keys
    | Unselect
    | Nop

type ObjectType = 
    | Sphere of V3d * float 
    | Box of Box3d

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
        [<NonIncremental>]
        otherObjects    : ISg<Action>
    }

[<DomainType>]
type Scene =
    {
        world  : World
        kind   : TrafoKind
        mode   : TrafoMode
        camera : CameraControllerState
        dockConfig : DockConfig
    }