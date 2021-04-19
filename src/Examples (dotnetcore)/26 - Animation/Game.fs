namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Anewmation
open FSharp.Data.Adaptive
open Aether
open Aether.Operators

module private Lens =

    let scene = Model.state_ >-> GameState.scene_
    let selected = scene >-> Scene.selected_

    let entity id =
        let lens = Model.state_ >-> GameState.scene_ >-> Scene.entities_
        (fun model -> model |> Optic.get lens |> HashMap.find id),
        (fun value model -> model |> Optic.map lens (HashMap.add id value))

    module V3d =
        let z : Lens<V3d, float> =
            (fun v -> v.Z),
            (fun z v -> V3d(v.XY, z))

    module Entity =
        let flag id = entity id >-> Entity.flag_
        let identity id = entity id >-> Entity.identity_
        let position id = entity id >-> Entity.position_
        let rotation id = entity id >-> Entity.rotation_
        let color id = entity id >-> Entity.color_
        let scale id = entity id >-> Entity.scale_


module private Slot =
    let private getName (slot : string) (entity : V2i) =
        Sym.ofString <| sprintf "%A/%s" entity slot

    let appearance = getName "appearance"
    let bobbing = getName "bobbing"
    let shake = getName "shake"
    let rotation = getName "rotation"

[<AutoOpen>]
module private ModelEntityExtensions =

    module Model =

        let getSelected =
            Optic.get Lens.selected

        let getFlag id =
            Optic.get (Lens.Entity.flag id)

        let getIdentity id =
            Optic.get (Lens.Entity.identity id)

        let getPosition id =
            Optic.get (Lens.Entity.position id)

        let getScale id =
            Optic.get (Lens.Entity.scale id)

        let isActive id =
            Optic.get (Lens.Entity.flag id) >> ((=) Active)

        let isHovered id =
            Optic.get (Lens.Entity.flag id) >> ((=) Hovered)

        let isSelected id =
            Optic.get (Lens.Entity.flag id) >> ((=) Selected)

        let isProcessing id =
            Optic.get (Lens.Entity.flag id) >> ((=) Processing)

        let isResolved id =
            Optic.get (Lens.Entity.flag id) >> ((=) Resolved)

        let getSelectedIdentity (model : Model) =
            model |> Optic.get Lens.selected |> Option.map getIdentity

        let allowHover id model =
            match model |> getFlag id with
            | Active -> true
            | _ -> false

        let allowSelect id model =
            match model |> getFlag id with
            | Active | Hovered -> true
            | _ -> false


module Animations =

    let bob (id : V2i) =
        let lens = Lens.Entity.position id >-> Lens.V3d.z

        Animation.Primitives.lerp -0.05 0.05
        |> Animation.ease (Easing.InOut EasingFunction.Sine)
        |> Animation.loop LoopMode.Mirror
        |> Animation.seconds 1.5
        |> Animation.link lens

    let shake (amplitude : float) (id : V2i) =
        let lens = Lens.Entity.rotation id >-> Lens.V3d.z

        [0.0; amplitude; -amplitude; amplitude; -amplitude; 0.0]
        |> Animation.Primitives.linearPath (fun x y -> abs (x - y))
        |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
        |> Animation.seconds 1.0
        |> Animation.link lens
        |> Animation.onFinalize (fun name _ model ->
            if model |> Model.isHovered id then
                model |> Animator.start name
            else
                model
        )

    let rotateToNeutral (id : V2i) =
        let lens = Lens.Entity.rotation id
        Animation.Primitives.lerpTo lens V3d.Zero
        >> Animation.milliseconds 200

    let fade (color : C3d) (id : V2i) =
        let lens = Lens.Entity.color id
        Animation.Primitives.lerpTo lens color
        >> Animation.ease (Easing.Out EasingFunction.Quadratic)

    let fadeHover (id : V2i) =
        fade Scene.Properties.Tile.colorHovered id
        >> Animation.milliseconds 75

    let fadeNeutral (id : V2i) =
        fade Scene.Properties.Tile.colorNeutral id
        >> Animation.seconds 1

    let fadeReveal (id : V2i) (model : Model) =
        let color = model |> Optic.get (Lens.Entity.identity id)

        model
        |> fade color id
        |> Animation.seconds 0.25

    let scale (scale : V3d) (id : V2i) =
        let lens = Lens.Entity.scale id
        Animation.Primitives.lerpTo lens scale

    let scaleToCube (id : V2i) =
        scale Scene.Properties.Cube.size id
        >> Animation.seconds 0.65
        >> Animation.ease (Easing.Out <| EasingFunction.Bounce 1.5)

    let scaleToTile (id : V2i) =
        scale Scene.Properties.Tile.size id
        >> Animation.seconds 1
        >> Animation.ease (Easing.Out <| EasingFunction.Elastic (1.8, 0.5))

    let reveal (id : V2i) (model : Model) =
        let color = model |> fadeReveal id
        let shape = model |> scaleToCube id
        Animation.concurrent [color; shape]

    let hide (id : V2i) (model : Model) =
        let color = model |> fadeNeutral id
        let shape = model |> scaleToTile id
        Animation.concurrent [color; shape]

    let private orbit (center : V2d) (position : V2d) (a : float) (b : float) =
        let radius = Vec.distance center position
        let diff = position - center
        let alpha = atan2 diff.Y diff.X

        Animation.create (fun t ->
            let angle = alpha + lerp a b t
            let x = center.X + radius * cos angle
            let y = center.Y + radius * sin angle
            V2d(x, y)
        )

    let private revolve (id : V2i) (other : V2i) (model : Model) =
        let position = model |> Model.getPosition id
        let center = (position + Model.getPosition other model) * 0.5

        let duration = 1.0
        let factor = 6.0
        let full = Constant.PiTimesTwo / factor

        let accel =
            orbit center.XY position.XY 0.0 full
            |> Animation.ease (Easing.In EasingFunction.Quadratic)
            |> Animation.seconds (duration / factor)

        let orbit =
            orbit center.XY position.XY full (full + Constant.PiTimesFour)
            |> Animation.loop LoopMode.Repeat
            |> Animation.seconds duration

        Animation.path [accel; orbit]

    let resolve (id : V2i) (other : V2i) (model : Model) =

        let ascendAndRevolve =
            let ascend =
                Animation.Primitives.lerp (model |> Model.getPosition id |> Vec.z) 12.0

            let revolve =
                model |> revolve id other

            (revolve, ascend)
            ||> Animation.input (fun xy z -> V3d(xy, z))
            |> Animation.link (Lens.Entity.position id)

        let shrink =
            Animation.Primitives.lerp (model |> Model.getScale id) V3d.Zero
            |> Animation.link (Lens.Entity.scale id)

        Animation.concurrent [ascendAndRevolve; shrink]
        |> Animation.ease (Easing.In EasingFunction.Quadratic)
        |> Animation.seconds 1.0


module Transitions =

    let initialize (model : Model) =
        let rnd = RandomSystem()

        (model, Scene.entityIndices) ||> List.fold (fun model id ->
            let addBob model =
                let name = Slot.bobbing id
                let anim = Animations.bob id
                let pos = rnd.UniformDouble()
                model |> Animator.createAndStartFrom name anim pos

            let addShake model =
                let name = Slot.shake id
                let anim = Animations.shake (Constant.Pi / 10.0) id
                model |> Animator.create name anim

            model |> addBob |> addShake
        )

    let hover (id : V2i) (model : Model) =
        model
        |> Animator.pause (Slot.bobbing id)
        |> Animator.start (Slot.shake id)
        |> Animator.createAndStart (Slot.appearance id) (model |> Animations.fadeHover id)

    let unhover (id : V2i) (model : Model) =
        model
        |> Animator.resume (Slot.bobbing id)
        |> Animator.createAndStartDelayed (Slot.appearance id) (Animations.fadeNeutral id)

    let reveal (id : V2i) (model : Model) =
        model
        |> Animator.stop (Slot.shake id)
        |> Animator.resume (Slot.bobbing id)
        |> Animator.createAndStart (Slot.rotation id) (model |> Animations.rotateToNeutral id)
        |> Animator.createAndStart (Slot.appearance id) (model |> Animations.reveal id)

    let revealAndHide (id : V2i) (sel : V2i) (model : Model) =

        let hideSelected (model : Model) =
            let hide =
                Animations.hide sel model
                |> Animation.onFinalize (fun _ _ ->
                    Optic.set (Lens.Entity.flag sel) Active
                )

            model |> Animator.createAndStart (Slot.appearance sel) hide

        model
        |> reveal id
        |> Animator.createAndStartDelayed (Slot.appearance id) (fun model ->
            let delay = Animation.empty |> Animation.seconds 0.75
            let hide =
                Animations.hide id model
                |> Animation.onStart (fun _ _ -> hideSelected)

            Animation.sequential [delay; hide]
            |> Animation.onFinalize (fun _ _ ->
                Optic.set (Lens.Entity.flag id) Active
            )
        )

    let revealAndResolve (id : V2i) (sel : V2i) (model : Model) =
        let resolve (self : V2i) (other : V2i) (model : Model) =
            let animation =
                Animations.resolve self other model
                |> Animation.onFinalize (fun _ _ ->
                    Optic.set (Lens.Entity.flag self) Resolved
                )

            model
            |> Animator.stop (Slot.bobbing self)
            |> Animator.createAndStart (Slot.appearance self) animation

        model
        |> reveal id
        |> Animator.createAndStartDelayed (Slot.appearance id) (fun _ ->
            Animation.empty
            |> Animation.seconds 0.3
            |> Animation.onFinalize (fun _ _ ->
                resolve id sel >> resolve sel id
            )
        )


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Game =

    let update (model : Model) (message : GameMessage) =
        match message with
        | Initialize ->
            model |> Transitions.initialize

        | Hover id when (model |> Model.allowHover id) ->
            model
            |> Transitions.hover id
            |> Optic.set (Lens.Entity.flag id) Hovered

        | Unhover id when (model |> Model.isHovered id) ->
            model
            |> Transitions.unhover id
            |> Optic.set (Lens.Entity.flag id) Active

        | Select id when (model |> Model.allowSelect id) ->
            let c = model |> Model.getIdentity id

            match model |> Model.getSelected with
            | Some s when c = (model |> Model.getIdentity s) ->
                model
                |> Transitions.revealAndResolve id s
                |> Optic.set Lens.selected None
                |> Optic.set (Lens.Entity.flag id) Processing
                |> Optic.set (Lens.Entity.flag s) Processing

            | Some s ->
                model
                |> Transitions.revealAndHide id s
                |> Optic.set Lens.selected None
                |> Optic.set (Lens.Entity.flag id) Processing
                |> Optic.set (Lens.Entity.flag s) Processing

            | _ ->
                model
                |> Transitions.reveal id
                |> Optic.set Lens.selected (Some id)
                |> Optic.set (Lens.Entity.flag id) Selected

        | _ ->
            model


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