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

    let private zLens : Lens<V3d, float> =
        (fun v -> v.Z),
        (fun z v -> V3d(v.XY, z))

    let private getEntityHeightLens (id : V2i) =
        getEntityLens id >-> Entity.position_ >-> zLens

    let private getEntityYawLens (id : V2i) =
        getEntityLens id >-> Entity.rotation_ >-> zLens

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Game =

    let update (model : Model) (message : GameMessage) =
        match message with
        | Initialize ->
            let rnd = RandomSystem()

            (model, Scene.entityIndices) ||> List.fold (fun model id ->

                let addBob model =
                    let name = AnimationId.get id "bob"
                    let anim = Animations.bob id
                    let pos = rnd.UniformDouble()
                    model |> Animator.set name anim |> Animator.startFrom name pos

                let addShake model =
                    let name = AnimationId.get id "shake"
                    let anim = id |> Animations.shake (Constant.Pi / 10.0)
                    model |> Animator.set name anim |> Animator.start name |> Animator.pause name

                model
                |> addBob
                |> addShake
            )

        | Hover id ->
            let bob = AnimationId.get id "bob"
            let shake = AnimationId.get id "shake"

            model
            |> Animator.pause bob
            |> Animator.start shake
            |> Optic.set Lens.hovered (Some id)

        | Unhover ->
            match model |> Optic.get Lens.hovered with
            | Some id ->
                let bob = AnimationId.get id "bob"
                model
                |> Animator.resume bob
                |> Optic.set Lens.hovered None

            | _ ->
                model
