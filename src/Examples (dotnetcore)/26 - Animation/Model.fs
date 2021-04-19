namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.UI.Anewmation
open FSharp.Data.Adaptive
open Adaptify

[<ModelType>]
type EntityFlag =
    | Active
    | Selected
    | Hovered
    | Processing
    | Resolved

[<ModelType>]
type Entity =
    {
        position : V3d
        rotation : V3d
        scale : V3d
        color : C3d
        flag : EntityFlag

        [<NonAdaptive>]
        identity : C3d
    }

[<ModelType>]
type Scene =
    {
        entities : HashMap<V2i, Entity>
        lightDirection : V3d
        selected : V2i option
    }

[<ModelType>]
type Score =
    {
        current : int
        displayed : int
        color : C3d
        scale : V2d
    }

[<ModelType>]
type GameState =
    {
        scene : Scene
        score : Score
        allowCameraInput : bool
    }

[<ModelType>]
type Model =
    {
        state : GameState
        camera : OrbitState
        [<NonAdaptive>]
        animator : Animator<Model>
    }

type GameMessage =
    | Initialize
    | Select of V2i
    | Hover of V2i
    | Unhover of V2i

type Message =
    | Game of GameMessage
    | Camera of OrbitMessage
    | Animation of AnimatorMessage<Model>