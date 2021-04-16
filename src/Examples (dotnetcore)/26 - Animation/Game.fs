namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Anewmation
open FSharp.Data.Adaptive
open Aether
open Aether.Operators

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Lens =
    let scene = Model.state_ >-> GameState.scene_
    let hovered = scene >-> Scene.hovered_

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GameState =

    let score =
        { current = 0
          displayed = 0
          color = C3d.White
          scale = V2d.One }

    let initial =
        { scene = Scene.initial
          score = score
          allowCameraInput = true }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AnimationId =

    let get (entity : V2i) (name : string) =
        Sym.ofString <| sprintf "%A/%s" entity name

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Animations =

    let private getEntityLens (id : V2i) =
        let lens = Model.state_ >-> GameState.scene_ >-> Scene.entities_
        (fun model -> model |> Optic.get lens |> HashMap.find id),
        (fun value model -> model |> Optic.map lens (HashMap.add id value))

    let private xzLens : Lens<V3d, V2d> =
        (fun v -> v.XZ),
        (fun xz v -> V3d(xz.X, v.Y, xz.Y))

    let private zLens : Lens<V3d, float> =
        (fun v -> v.Z),
        (fun z v -> V3d(v.XY, z))

    let private getEntityHeightLens (id : V2i) =
        getEntityLens id >-> Entity.position_ >-> zLens

    let private getEntityYawLens (id : V2i) =
        getEntityLens id >-> Entity.rotation_ >-> zLens

    let private getEntityColorLens (id : V2i) =
        getEntityLens id >-> Entity.color_

    let bob (id : V2i) =
        let lens = getEntityHeightLens id

        Animation.Primitives.lerp -0.05 0.05
        |> Animation.ease (Easing.InOut EasingFunction.Sine)
        |> Animation.loop LoopMode.Mirror
        |> Animation.seconds 1.5
        |> Animation.link lens

    let shake (amplitude : float) (id : V2i) =
        let lens = getEntityYawLens id

        [0.0; amplitude; -amplitude; amplitude; -amplitude; 0.0]
        |> Animation.Primitives.linearPath (fun x y -> abs (x - y))
        |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
        |> Animation.seconds 1.0
        |> Animation.link lens
        |> Animation.onFinalize (fun name _ model ->
            match model |> Optic.get Lens.hovered with
            | Some hovered when hovered = id -> model |> Animator.start name
            | _ -> model
        )

    let private fade (color : C3d) (id : V2i) =
        let lens = getEntityColorLens id
        Animation.Primitives.lerpTo lens color
        >> Animation.ease (Easing.Out EasingFunction.Quadratic)

    let fadeHover (id : V2i) =
        fade Scene.Properties.Box.colorHovered id
        >> Animation.milliseconds 75

    let fadeUnhover (id : V2i) =
        fade Scene.Properties.Box.color id
        >> Animation.seconds 2

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Transitions =

    let initialize (model : Model) =
        let rnd = RandomSystem()

        (model, Scene.entityIndices) ||> List.fold (fun model id ->
            let addBob model =
                let name = AnimationId.get id "bob"
                let anim = Animations.bob id
                let pos = rnd.UniformDouble()
                model |> Animator.createAndStartFrom name anim pos

            let addShake model =
                let name = AnimationId.get id "shake"
                let anim = Animations.shake (Constant.Pi / 10.0) id
                model |> Animator.create name anim

            model |> addBob |> addShake
        )

    let hover (id : V2i) (model : Model) =
        let bob = AnimationId.get id "bob"
        let shake = AnimationId.get id "shake"
        let fade = AnimationId.get id "fade"

        model
        |> Animator.pause bob
        |> Animator.start shake
        |> Animator.createAndStart fade (model |> Animations.fadeHover id)

    let unhover (id : V2i) (model : Model) =
        let bob = AnimationId.get id "bob"
        let fade = AnimationId.get id "fade"

        model
        |> Animator.resume bob
        |> Animator.createAndStartDelayed fade (Animations.fadeUnhover id)
        |> Optic.set Lens.hovered None

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Game =

    let update (model : Model) (message : GameMessage) =
        match message with
        | Initialize ->
            model |> Transitions.initialize

        | Hover id when not (model |> Optic.get Lens.hovered |> Option.contains id) ->
            model
            |> Transitions.hover id
            |> Optic.set Lens.hovered (Some id)

        | Unhover ->
            match model |> Optic.get Lens.hovered with
            | Some id ->
                model
                |> Transitions.unhover id
                |> Optic.set Lens.hovered None

            | _ ->
                model

        | _ ->
            model