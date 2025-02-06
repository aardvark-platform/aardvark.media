namespace AdvancedAnimations

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Animation
open FSharp.Data.Adaptive
open Aether
open Aether.Operators

module private Lens =

    let camera = Model.camera_ >-> OrbitState.view_
    let state = Model.state_
    let selected = Model.scene_ >-> Scene.selected_

    let entity id =
        let lens = Model.scene_ >-> Scene.entities_
        (fun model -> model |> Optic.get lens |> HashMap.find id),
        (fun value model -> model |> Optic.map lens (HashMap.add id value))

    module Camera =
        let private location_ : Lens<CameraView, V3d> =
            (fun v -> v.Location),
            (fun l v -> v.WithLocation l)

        let location = camera >-> location_

    module V2d =
        let y : Lens<V2d, float> =
            (fun v -> v.Y),
            (fun y v -> V2d(v.X, y))

    module V3d =
        let z : Lens<V3d, float> =
            (fun v -> v.Z),
            (fun z v -> V3d(v.XY, z))

    module Caption =
        let text = Model.scene_ >-> Scene.caption_ >-> Caption.text_
        let position = Model.scene_ >-> Scene.caption_ >-> Caption.position_
        let size = Model.scene_ >-> Scene.caption_ >-> Caption.size_
        let scale = Model.scene_ >-> Scene.caption_ >-> Caption.scale_

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

    let camera = Sym.ofString "camera"
    let preparing = Sym.ofString "preparing"
    let caption = Sym.ofString "caption"
    let appearance = getName "appearance"
    let bobbing = getName "bobbing"
    let shake = getName "shake"
    let rotation = getName "rotation"

module private CameraPath =

    let points =
        let getPhi t = t |> lerp -Constant.PiHalf Constant.PiQuarter
        let getTheta t = t |> lerp (Constant.Pi * 0.4) Constant.PiQuarter
        let getRadius t = t |> lerp 64.0 24.0

        [| 0.0 .. 0.1 .. 1.0 |] |> Array.map (fun t ->
            let phi = getPhi t
            let theta = getTheta t
            let radius = getRadius t

            V2d(phi, theta).CartesianFromSpherical() * radius
        )

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

        let isHovered id =
            Optic.get (Lens.Entity.flag id) >> ((=) Hovered)

        let allowHover id model =
            match model |> getFlag id with
            | Active -> true
            | _ -> false

        let allowSelect id model =
            match model |> getFlag id with
            | Active | Hovered -> true
            | _ -> false


module Animations =

    let inline delay (delayInSeconds : ^Seconds) =
        Animation.empty |> Animation.seconds delayInSeconds

    let textFadeOut' (scale : V2d) =
        Animation.Primitives.lerp scale (V2d(1.2, 0.0))
        |> Animation.link Lens.Caption.scale
        |> Animation.milliseconds 75

    let textFadeIn =
        Animation.Primitives.lerp (V2d(1.2, 0.0)) V2d.One
        |> Animation.link Lens.Caption.scale
        |> Animation.milliseconds 75

    let textFadeOut =
        textFadeOut' V2d.One

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

    let flyover =

        let durationInSeconds = 5

        let location =
            CameraPath.points |> Animation.Primitives.smoothPath Vec.distance 0.1
            |> Animation.link Lens.Camera.location  // Note that we write the camera location here, so that the forward animation can read the value
            |> Animation.seconds durationInSeconds

        let forward =
            let turnDurationInSeconds = 1

            let turn =
                Animation.Camera.rotateDynamic V3d.Zero Lens.camera
                |> Animation.seconds turnDurationInSeconds

            let fixOnCenter =
                Animation.create' (fun model _ ->
                    let view = model |> Optic.get Lens.camera
                    CameraView.lookAt view.Location V3d.Zero view.Sky
                )
                |> Animation.seconds (durationInSeconds - turnDurationInSeconds)

            Animation.path [turn; fixOnCenter]
            |> Animation.map CameraView.forward

        (location, forward) ||> Animation.map2 (fun l f ->
            CameraView.look l f V3d.ZAxis
        )
        |> Animation.link Lens.camera
        |> Animation.seconds durationInSeconds
        |> Animation.ease (Easing.InOut EasingFunction.Quadratic)


module Transitions =

    let initialize (model : Model) =
        let rnd = RandomSystem()

        let addStaticAnimations model =
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

        let addBlinkingCaption model =
            let animation =
                Animation.Primitives.lerp 0.15 0.155
                |> Animation.link Lens.Caption.size
                |> Animation.ease (Easing.In EasingFunction.Cubic)
                |> Animation.loop LoopMode.Mirror
                |> Animation.seconds 1.0

            model
            |> Animator.createAndStart Slot.caption animation
            |> Optic.set Lens.Caption.text "Click to Start"
            |> Optic.set Lens.Caption.scale V2d.One
            |> Optic.set Lens.Caption.position (V2d (0.0, 0.75))

        let setCamera model =
            let view = CameraView.look CameraPath.points.[0] V3d.XAxis V3d.ZAxis
            model |> Optic.set Lens.camera view

        model
        |> addStaticAnimations
        |> addBlinkingCaption
        |> setCamera
        |> Optic.set Lens.state Introduction

    let hover (id : V2i) (model : Model) =
        model
        |> Animator.pause (Slot.bobbing id)
        |> Animator.start (Slot.shake id)
        |> Animator.createAndStart (Slot.appearance id) (model |> Animations.fadeHover id)
        |> Optic.set (Lens.Entity.flag id) Hovered

    let unhover (id : V2i) (model : Model) =
        model
        |> Animator.resume (Slot.bobbing id)
        |> Animator.createAndStartDelayed (Slot.appearance id) (Animations.fadeNeutral id)
        |> Optic.set (Lens.Entity.flag id) Active

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
            let delay = Animations.delay 0.75
            let hide =
                Animations.hide id model
                |> Animation.onStart (fun _ _ -> hideSelected)

            Animation.sequential [delay; hide]
            |> Animation.onFinalize (fun _ _ ->
                Optic.set (Lens.Entity.flag id) Active
            )
        )
        |> Optic.set (Lens.Entity.flag id) Processing
        |> Optic.set (Lens.Entity.flag sel) Processing

    let revealAndResolve (id : V2i) (sel : V2i) (model : Model) =
        let isFinished = (model.state = Finished)

        let showFinishScreen (model : Model) =
            model
            |> Optic.set Lens.Caption.text "Finished!"
            |> Animator.createAndStart Slot.caption Animations.textFadeIn

        let resolve (self : V2i) (other : V2i) (model : Model) =
            let animation =
                Animations.resolve self other model
                |> Animation.onFinalize (fun _ _ ->
                    Optic.set (Lens.Entity.flag self) Resolved
                    >> if isFinished then showFinishScreen else Operators.id
                )

            model
            |> Animator.stop (Slot.bobbing self)
            |> Animator.createAndStart (Slot.appearance self) animation

        model
        |> reveal id
        |> Animator.createAndStartDelayed (Slot.appearance id) (fun _ ->
            Animations.delay 0.3
            |> Animation.onFinalize (fun _ _ ->
                resolve id sel >> resolve sel id
            )
        )
        |> Optic.set (Lens.Entity.flag id) Processing
        |> Optic.set (Lens.Entity.flag sel) Processing

    let private revealAll (model : Model) =
        let reveal =
            Scene.entityIndices |> List.mapi (fun i id ->
                let delay = Animations.delay (float (i + 1) * 0.1)
                let reveal =
                    Animations.reveal id model
                    |> Animation.onStart (fun _ _ model ->
                        model
                        |> Animator.stop (Slot.shake id)
                        |> Animator.resume (Slot.bobbing id)
                        |> Animator.createAndStart (Slot.rotation id) (model |> Animations.rotateToNeutral id)
                    )

                Animation.sequential [delay; reveal] :> IAnimation<Model>
            )

        let showStart =
            Animations.textFadeOut
            |> Animation.onFinalize (fun _ _ ->
                Optic.set Lens.Caption.text "START!"
                >> Animator.createAndStart Slot.caption Animations.textFadeIn
            )

        let hide model =
            let delay = Animations.delay 3

            let hide =
                Scene.entityIndices |> List.map (fun id ->
                    Animations.hide id model :> IAnimation<Model>
                )
                |> Animation.concurrent
                |> Animation.onStart (fun _ _ ->
                    Animator.createAndStart Slot.caption showStart
                )

            Animation.sequential [delay; hide]
            |> Animation.onFinalize (fun _ _ ->
                Optic.set Lens.state (Running 0)
                >> Animator.createAndStart Slot.caption (Animations.textFadeOut' V2d.One)
            )

        let animation =
            Animation.concurrent reveal
            |> Animation.onFinalize (fun name _ model ->
                model
                |> Animator.createAndStart name (hide model)
            )

        model |> Animator.createAndStart Slot.preparing animation

    let private prepare (model : Model) =
        let animation =
            Animation.sequential [
                Animations.delay 0.25
                Animations.textFadeIn
            ]

        model
        |> revealAll
        |> Optic.set Lens.Caption.text "Remember and Find the Pairs!"
        |> Animator.createAndStart Slot.caption animation

    let flyover (model : Model) =
        let animation =
            Animations.flyover
            |> Animation.onFinalize (fun _ _ -> Optic.set Model.state_ Preparing >> prepare)

        model
        |> Animator.createAndStart Slot.camera animation
        |> Animator.createAndStart Slot.caption (Animations.textFadeOut' (model |> Optic.get Lens.Caption.scale))
        |> Optic.set Lens.state Flyover

module Game =

    let update (model : Model) (message : GameMessage) =
        match message with
        | Initialize ->
            model |> Transitions.initialize

        | Start when model.state = Introduction ->
            model |> Transitions.flyover

        | Pause ->
            match model.state with
            | Paused original ->
                { model with state = original }
                |> Animator.iterate (fun a -> a.Resume())

            | _ ->
                { model with state = Paused model.state }
                |> Animator.iterate (fun a -> a.Pause())

        | _ when not <| GameState.isRunning model.state ->
            model

        | Hover id when (model |> Model.allowHover id) ->
            model |> Transitions.hover id

        | Unhover id when (model |> Model.isHovered id) ->
            model |> Transitions.unhover id

        | Select id when (model |> Model.allowSelect id) ->
            let c = model |> Model.getIdentity id

            match model |> Model.getSelected with
            | Some s when c = (model |> Model.getIdentity s) ->
                model
                |> Optic.set Lens.selected None
                |> Optic.map Lens.state (function
                    | Running r when r < Scene.entityIndices.Length / 2 - 1 -> Running (r + 1)
                    | _ -> Finished
                )
                |> Transitions.revealAndResolve id s

            | Some s ->
                model
                |> Transitions.revealAndHide id s
                |> Optic.set Lens.selected None

            | _ ->
                model
                |> Transitions.reveal id
                |> Optic.set Lens.selected (Some id)
                |> Optic.set (Lens.Entity.flag id) Selected

        | _ ->
            model