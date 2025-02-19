﻿namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.UI.Animation
open Aardvark.Application
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
type Caption =
    {
        text : string
        position : V2d
        size : float
        scale : V2d
    }

[<ModelType>]
type Scene =
    {
        entities : HashMap<V2i, Entity>
        lightDirection : V3d
        selected : V2i option
        caption : Caption
    }

[<ModelType>]
type GameState =
    | Introduction
    | Flyover
    | Preparing
    | Running of resolved: int
    | Finished
    | Paused of original: GameState

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GameState =

    let isRunning = function
        | Running _ -> true | _ -> false

    let isInteractive = function
        | Preparing | Running _ -> true | _ -> false

[<ModelType>]
type Model =
    {
        scene : Scene
        state : GameState
        camera : OrbitState
        [<NonAdaptive>]
        animator : Animator<Model>
    }

type GameMessage =
    | Initialize
    | Start
    | Select of V2i
    | Hover of V2i
    | Unhover of V2i
    | Pause

type Message =
    | OnKeyDown of Keys
    | Game of GameMessage
    | Camera of OrbitMessage
    | Animation of AnimatorMessage<Model>