﻿namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Animation
open FSharp.Data.Adaptive
open Aardvark.Application

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    let initial lens =
        { scene = Scene.initial
          state = Introduction
          camera = OrbitState.create V3d.Zero Constant.PiQuarter Constant.PiQuarter 24.0
          animator = Animator.initial lens }

module App =

    [<AutoOpen>]
    module private Internal =
        open Aardvark.Service

        let view (model : AdaptiveModel) =

            let renderControl =
                let frustum = AVal.constant <| Frustum.perspective 60.0 0.1 100.0 1.0
                let sg (cv : ClientValues) = model.scene |> Scene.view cv.runtime cv.size |> Sg.map Game

                sg |> OrbitController.controlledControlWithClientValues model.camera Camera frustum (AttributeMap.ofList [
                    style "width: 100%; height:100%"
                    onEvent "onRendered" [] (fun _ -> Animation AnimatorMessage.RealTimeTick)
                    onClick (fun _ -> GameMessage.Start |> Game)
                    onKeyDown OnKeyDown
                    attribute "showFPS" "true"
                    attribute "data-samples" "8"
                ]) RenderControlConfig.standard

            renderControl

        let threads (model : Model) =
            let camera = OrbitController.threads model.camera |> ThreadPool.map Camera
            let animation = Animator.threads model.animator |> ThreadPool.map Animation
            ThreadPool.union camera animation


    let rec update (model : Model) (msg : Message) =
        match msg with
        | Game m ->
            m |> Game.update model

        | Camera m when GameState.isInteractive model.state ->
            { model with camera = m |> OrbitController.update model.camera }

        | Animation msg ->
            model |> Animator.update msg

        | OnKeyDown Keys.Space ->
            Game GameMessage.Pause |> update model

        | _ ->
            model

    let app : App<_,_,_> =
        let model = Model.initial Model.animator_

        {
            unpersist = Unpersist.instance
            threads = threads
            initial = Game GameMessage.Initialize |> update model
            update = update
            view = view
        }
