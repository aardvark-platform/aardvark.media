module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Anewmation
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open RenderControl.Model

open System.Diagnostics


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

                let rec dewit model =
                    //model
                    //|> Animation.lerpTo Model.color_ (if model.color = C4b.Green then C4b.Red else C4b.Green)
                    Animation.lerp (C4d model.color) (if model.color = C4b.Green then C4d.Red else C4d.Green)
                    |> Animation.map C4b
                    |> Animation.link Model.color_
                    |> Animation.seconds 5
                    |> Animation.ease (Easing.InOut EasingFunction.Cubic)
                    |> Animation.onStart (fun _ ->      Log.warn "[Color] started")
                    |> Animation.onStop (fun _ ->       Log.warn "[Color] stopped")
                    |> Animation.onPause (fun _ ->      Log.warn "[Color] paused")
                    |> Animation.onResume (fun _ ->     Log.warn "[Color] resumed")
                    |> Animation.onFinalize (Log.warn "[Color] finished: %A")
                    |> Animation.onFinalize' (fun name _ model ->
                        model |> Animator.set name (dewit model)
                    )

                dewit model

            let rotationAnimation =
                let rotX =
                    model |> Animation.lerpTo Model.rotationX_ (if model.rotationX = 0.0 then Constant.Pi else 0.0)
                    |> Animation.seconds 2
                    |> Animation.ease (Easing.InOut EasingFunction.Sine)

                let rotZ =
                    model |> Animation.lerpAngleTo Model.rotationZ_ (if model.rotationZ = 0.0f then (-ConstantF.PiHalf + ConstantF.PiTimesTwo) else 0.0f)
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

            let animation =
                [colorAnimation] |> Animation.ofList
                |> Animation.andAlso rotationAnimation
                |> Animation.subscribe timer
                |> Animation.loop LoopMode.Mirror


            model |> Animator.set animSym animation

    | Camera m ->
        { model with cameraState = FreeFlyController.update model.cameraState m }
    | CenterScene ->
        { model with cameraState = initialCamera }
    | Animation msg ->
        model |> Animator.update msg

let viewScene (model : AdaptiveModel) =
    Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.Zero, V3d.One))
    |> Sg.uniform "Color" model.color
    |> Sg.trafo (model.rotation |> AVal.map Trafo3d)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.sgColor
        do! DefaultSurfaces.simpleLighting
    }


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
