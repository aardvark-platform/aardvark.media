namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.UI.Anewmation
open FSharp.Data.Adaptive
open Adaptify

[<ModelType>]
type Entity =
    {
        position : V3d
        rotation : V3d
        scale : V3d
        color : C3d
        alpha : float
    }

[<ModelType>]
type Scene =
    {
        entities : HashMap<V2i, Entity>
        lightDirection : V3d
        selected : V2i option
        hovered : V2i option
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
        animator : Animator<Model>
    }

type GameMessage =
    | Initialize
    | Hover of V2i
    | Unhover

type Message =
    | Game of GameMessage
    | Camera of OrbitMessage
    | Animation of AnimatorMessage<Model>