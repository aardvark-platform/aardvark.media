module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Anewmation
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open RenderControl.Model

open System.Diagnostics

open Aether
open Aether.Operators

let initialCamera = {
        FreeFlyController.initial with
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let animSym = Sym.ofString "colorAndRotationAnim"

let update (model : Model) (msg : Message) =
    match msg with
    | ToggleBackground ->
        { model with background = (if model.background = C4b.Black then C4b.White else C4b.Black) }
    | ToggleAnimation ->
        if model |> Animator.exists animSym then
            model |> Animator.start animSym
        else

            let timer =
                let stopwatch = Stopwatch.StartNew()

                let start _ =
                    stopwatch.Restart()
                    Log.warn "[Group] started"

                let finalize _ =
                    Log.warn "[Group] finished after %fms" stopwatch.Elapsed.TotalMilliseconds

                Observer.empty
                |> Observer.onStart start
                |> Observer.onFinalize finalize
                |> Observer.onStop (fun _ ->       Log.warn "[Group] stopped")
                |> Observer.onPause (fun _ ->      Log.warn "[Group] paused")
                |> Observer.onResume (fun _ ->     Log.warn "[Group] resumed")

            let colorAnimation =
                Animation.Primitives.lerp (C4d model.color) (if model.color = C4b.Green then C4d.Red else C4d.Green)
                |> Animation.map C4b
                |> Animation.link Model.color_
                |> Animation.seconds 5
                |> Animation.ease (Easing.InOut EasingFunction.Cubic)
                |> Animation.onStart (fun _ ->      Log.warn "[Color] started")
                |> Animation.onStop (fun _ ->       Log.warn "[Color] stopped")
                |> Animation.onPause (fun _ ->      Log.warn "[Color] paused")
                |> Animation.onResume (fun _ ->     Log.warn "[Color] resumed")
                |> Animation.onFinalize (Log.warn "[Color] finished: %A")
                |> Animation.loop LoopMode.Mirror

            let rotationAnimation =
                let rotX =
                    model |> Animation.Primitives.lerpTo Model.rotationX_ (if model.rotationX = 0.0 then Constant.Pi else 0.0)
                    |> Animation.seconds 2
                    |> Animation.ease (Easing.InOut EasingFunction.Sine)

                let rotZ =
                    model |> Animation.Primitives.lerpAngleTo Model.rotationZ_ (if model.rotationZ = 0.0f then (-ConstantF.PiHalf + ConstantF.PiTimesTwo) else 0.0f)
                    |> Animation.seconds 2
                    |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
                    |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
                    |> Animation.ease (Easing.InOut EasingFunction.Quadratic)

                (rotX, rotZ) ||> Animation.map2 (fun roll yaw ->
                    Rot3d.RotationEuler(roll, 0.0, float yaw)
                )
                |> Animation.link Model.rotation_
                |> Animation.onStart (fun _ ->      Log.warn "[Rotation] started")
                |> Animation.onStop (fun _ ->       Log.warn "[Rotation] stopped")
                |> Animation.onPause (fun _ ->      Log.warn "[Rotation] paused")
                |> Animation.onResume (fun _ ->     Log.warn "[Rotation] resumed")
                |> Animation.onFinalize (fun _ ->   Log.warn "[Rotation] finished")
                |> Animation.loop LoopMode.Mirror


            let cameraAnimation =
                let lens = Model.cameraState_ >-> CameraControllerState.view_
                let view1 = initialCamera.view
                let view2 = CameraView.lookAt (V3d(-3.0, 12.0, 6.0)) V3d.OOO V3d.OOI
                let view3 = CameraView.lookAt (V3d(-5.0, -16.0, 2.0)) V3d.OOO V3d.OOI

                let rec next model =
                    let src = model |> Optic.get lens
                    let dst =
                        if src.Location = view1.Location then view2
                        elif src.Location = view2.Location then view3
                        elif src.Location = view3.Location then view1
                        else view2

                    model |> Animation.Camera.interpolateTo lens dst
                    |> Animation.seconds 2
                    |> Animation.ease (Easing.InOut EasingFunction.Quadratic)
                    |> Animation.onFinalize' (fun name _ model ->
                        model |> Animator.set name (next model)
                    )

                next model

            let animation =
                cameraAnimation
                |> Animation.andAlso rotationAnimation
                |> Animation.andAlso colorAnimation
                |> Animation.subscribe timer


            model |> Animator.set animSym animation

    | Camera m ->
        { model with cameraState = FreeFlyController.update model.cameraState m }
    | CenterScene ->
        { model with cameraState = initialCamera }
    | Animation msg ->
        model |> Animator.update msg

let viewScene (model : AdaptiveModel) =
    let floor =
        Sg.quad
        |> Sg.diffuseTexture DefaultTextures.checkerboard
        |> Sg.scale 10.0
        |> Sg.translate 0.0 0.0 -2.0
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
        }

    let box =
        Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.Zero, V3d.One))
        |> Sg.uniform "Color" model.color
        |> Sg.trafo (model.rotation |> AVal.map Trafo3d)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.sgColor
            do! DefaultSurfaces.simpleLighting
        }

    Sg.ofList [floor; box]


let view (model : AdaptiveModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant)
                    (AttributeMap.ofListCond [
                        always <| style "width: 100%; grid-row: 2; height:100%";
                        always <| attribute "showFPS" "true";         // optional, default is false
                        "style", model.background |> AVal.map(fun c -> sprintf "background: #%02X%02X%02X" c.R c.G c.B |> AttributeValue.String |> Some)
                        //attribute "showLoader" "false"    // optional, default is true
                        //attribute "data-renderalways" "1" // optional, default is incremental rendering
                        always <| attribute "data-samples" "8"        // optional, default is 1
                    ])
            (viewScene model)

    let colorAnimationPaused =
        model.animator |> Animator.isPaused' animSym

    let colorAnimationRunning =
        model.animator |> Animator.isRunning' animSym

    div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
        div [style "grid-row: 1"] [
            text "Animation controls"
            br []
            Incremental.div AttributeMap.empty <| alist {
                let! paused = colorAnimationPaused
                let! running = colorAnimationRunning

                if not running then
                    if not paused then
                        button [onClick (fun _ -> ToggleAnimation)] [text "Start"]
                    else
                        button [onClick (fun _ -> ToggleAnimation)] [text "Restart"]
                        button [onClick (fun _ -> Animation (AnimatorMessage.Stop(animSym, true)))] [text "Stop"]
                        button [onClick (fun _ -> Animation (AnimatorMessage.Resume animSym))] [text "Resume"]
                else
                    button [onClick (fun _ -> ToggleAnimation)] [text "Restart"]
                    button [onClick (fun _ -> Animation (AnimatorMessage.Stop(animSym, true)))] [text "Stop"]

                    if not paused then
                        button [onClick (fun _ -> Animation (AnimatorMessage.Pause animSym))] [text "Pause"]
                    else
                        button [onClick (fun _ -> Animation (AnimatorMessage.Resume animSym))] [text "Resume"]
            }
        ]
        renderControl
        br []
        text "use first person shooter WASD + mouse controls to control the 3d scene"
    ]

let threads (model : Model) =
    let camera = FreeFlyController.threads model.cameraState |> ThreadPool.map Camera
    let animation = Animator.threads model.animator |> ThreadPool.map Animation
    ThreadPool.union camera animation


let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial =
            {
               cameraState = initialCamera
               background = C4b.Black
               color = C4b.Green
               rotation = Rot3d.Identity
               rotationX = 0.0
               rotationZ = 0.0f
               animator = Animator.initial Model.animator_
            }
        update = update
        view = view
    }
