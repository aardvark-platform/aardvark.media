module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Generic

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.UI.Primitives.FreeFlyHeuristics
open Inc.Model


let setSpeedCoefficient (model : Model) (speed : float) =
    { model with cameraSpeed =  model.cameraSpeed.AdjustSpeedCoefficient(speed) }

let adjustMoveVelocity (model : Model) (unitsPerSecond : float) = 
    { model with cameraSpeed = model.cameraSpeed.AdjustToUnitsPerSecond(unitsPerSecond) }

let applyToCamera (v : SpeedHeuristic) (model : Model) =
    { model with freeflyCamera = { model.freeflyCamera with freeFlyConfig = v.Config }; cameraSpeed = v }

let getInitial (world : Box3d) =
    let radius = Vec.distance world.Max world.Center
    let view  = CameraView.lookAt world.Max world.Center V3d.OOI
    let freeFly = { FreeFlyController.initial with view = view }
    freeFly, OrbitState.ofFreeFly radius freeFly

let rec update (model : Model) (msg : Message) =
    match msg with
        | SetMode mode -> { model with mode = mode }
        | FreeFlyCameraMessage msg -> 
            let newFreeFly = FreeFlyController.update model.freeflyCamera msg 
            printfn "loc: %A" newFreeFly.view.Location
            { model with 
                freeflyCamera = newFreeFly
                orbitCamera   = OrbitState.ofFreeFly model.orbitCamera.radius newFreeFly 
            }
        | OrbitCameraMessage msg -> 
            let newOrbit = OrbitController.update model.orbitCamera msg 
            { model with 
                orbitCamera   = newOrbit
                freeflyCamera = OrbitState.toFreeFly model.cameraSpeed.SpeedCoefficient newOrbit
            }
        | IncreaseMoveSpeed ->
            let newSpeed = model.cameraSpeed.AdjustSpeedCoefficient(model.cameraSpeed.SpeedCoefficient + 0.1)
            applyToCamera newSpeed model

        | DecreaseMoveSpeed -> 
            let newSpeed = model.cameraSpeed.AdjustSpeedCoefficient(model.cameraSpeed.SpeedCoefficient - 0.1)
            applyToCamera newSpeed model

        | SetOrbitCenter center -> 
            let newOrbit = OrbitController.update model.orbitCamera (OrbitMessage.SetTargetCenter center)
            { model with 
                orbitCamera   = newOrbit
                freeflyCamera = OrbitState.toFreeFly model.cameraSpeed.SpeedCoefficient newOrbit
            }
        | SetCameraCoefficient speed -> 
             let newSpeed = model.cameraSpeed.AdjustSpeedCoefficient(speed)
             applyToCamera newSpeed model

        | AdjustMoveVelocity velocity -> 
            let newSpeed = model.cameraSpeed.AdjustToUnitsPerSecond(velocity)
            applyToCamera newSpeed model

        | ResetCamera -> 
            let bounds = Box3d.FromCenterAndSize(V3d.OOO, V3d.Half * model.worldSize)
            let freeFly, orbit = getInitial bounds
            { model with
                orbitCamera = orbit
                freeflyCamera = freeFly
            }


        | SetWorldSize size -> 
            let realSize =  pow 10.0 size
            if model.autoAdjustSpeed then
                let adjustedVelocity = realSize / 2.0 * 3.68 // 3.68 is normal speed (1.0) which works good for scenes of size 10
                let newSpeed =  model.cameraSpeed.AdjustToUnitsPerSecond(adjustedVelocity)
                applyToCamera newSpeed { model with  worldSize = realSize; }
            else
                { model with worldSize = realSize } 
        | ToggleAutoAdjustSpeed -> 
            { model with autoAdjustSpeed = not model.autoAdjustSpeed}

let view (model : AdaptiveModel) =

    let vertices, colors = 
        let rnd = new RandomSystem()
        let cnt = 80000
        let vertices = 
            model.worldSize |> AVal.map (fun worldSize -> 
                let bounds = Box3d.FromCenterAndSize(V3d.OOO, V3d.Half * worldSize)
                Array.init cnt (fun _ -> rnd.UniformV3d(bounds) |> V3f)
            )
        let colors =
            Array.init cnt (fun _ -> rnd.UniformC3f() |> C4b) |> AVal.constant

        vertices, colors

    let sg = 
        Sg.draw IndexedGeometryMode.PointList
        |> Sg.vertexAttribute DefaultSemantic.Positions vertices
        |> Sg.vertexAttribute DefaultSemantic.Colors colors
        |> Sg.uniform "PointSize" (AVal.constant 2.0)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.pointSprite
            do! DefaultSurfaces.pointSpriteFragment
        }
          
    let box = 
        model.worldSize |> AVal.map (fun worldSize -> 
            let box = Box3d.FromCenterAndSize(V3d.OOO, V3d.III * worldSize)
            printfn "world size box %A" box
            box
        )
    let sg' = 
        Sg.box (AVal.constant C4b.White) box
        |> Sg.requirePicking
        |> Sg.withEvents [
            Sg.onDoubleClick (fun pt -> SetOrbitCenter pt)
        ]
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }

    let coordinateCross = 
        Sg.coordinateCross (model.worldSize |> AVal.map (fun s -> s * 0.1))
        |> Sg.trafo (model.orbitCamera.center |> AVal.map Trafo3d.Translation)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
        }
        |> Sg.onOff (model.mode |> AVal.map (function CameraMode.Orbit -> true | _ -> false))

    let scene = 
        Sg.ofSeq [
            sg'
            coordinateCross
        ]

    let renderControl = 
        let frustum = Frustum.perspective 60.0 0.1 1000000.0 1.0 |> AVal.constant 
        let camera = (model.freeflyCamera.view, frustum) ||> AVal.map2 Camera.create
        let cameraAttributes =    
            amap {
                let! mode = model.mode
                match mode with
                | CameraMode.FreeFly -> 
                    yield! FreeFlyController.extractAttributes model.freeflyCamera FreeFlyCameraMessage
                | CameraMode.Orbit -> 
                    yield! OrbitController.extractAttributes model.orbitCamera OrbitCameraMessage
                | _ ->
                    failwith "not a camera mode"
            }
            |> AttributeMap.ofAMap
        let attributes = AttributeMap.ofList [ style "width: 100%; height: 100%"; ]
        Incremental.renderControl camera (AttributeMap.union cameraAttributes attributes) scene

    require Html.semui (
        div [clazz "ui"; ] [// style "background: #1B1C1E"] [
            renderControl
            div [ style "position: fixed; top: 20px; left: 20px"; clientEvent "onmouseenter" "$('#__ID__').animate({ opacity: 1.0 });";  clientEvent "onmouseleave" "$('#__ID__').animate({ opacity: 0.2 });" ] [

                table [ clazz "ui inverted table" ] [
                    tr [] [
                        let speed = 
                            model.freeflyCamera.freeFlyConfig.Current 
                            |> AVal.map FreeFlyController.computeMoveVelocity  
                        
                        Simple.labeledFloatInput''  "Free Fly Speed (units/s)" 0.0 100.0 0.1 AdjustMoveVelocity speed (sprintf "%.2f") (AttributeMap.ofList [ clazz "ui small labeled input"; style "width: 60pt"]) (AttributeMap.ofList [ clazz "ui label" ]) 
                        
                        br []
                        br []
                        checkbox (AttributeMap.ofList [ clazz "ui label" ])  model.autoAdjustSpeed ToggleAutoAdjustSpeed [text "Auto adjust camera speed"]
                        br []
                        br []
                        button [clazz "ui mini button"; onClick (fun _ -> ResetCamera)] [text "Reset Camera"]
                        br []
                        br []

                        //Html.SemUi.dropDown model.mode SetMode 
                        let enumValues = AMap.ofArray((System.Enum.GetValues typeof<CameraMode> :?> (CameraMode [])) |> Array.map (fun c -> (c, text (System.Enum.GetName(typeof<CameraMode>, c)) )))
                        dropdownUnclearable [ clazz "ui inverted selection dropdown" ] enumValues model.mode SetMode
                        br []
                        br []
                    ]
                ]
                    
                

                table [ clazz "ui inverted table" ] [

                    tr [] [
                        td [] "Free Fly Speed Coefficient (exp)"
                        td [ clazz "right aligned" ] (model.cameraSpeed |> AVal.map (fun speed -> sprintf " %.2f" speed.SpeedCoefficient))
                        td [] [ slider { min = -10.0; max = 12.0; step = 0.01;} [ clazz "ui inverted red slider"; style "width: 150px"] (model.cameraSpeed |> AVal.map (fun s -> s.SpeedCoefficient)) SetCameraCoefficient ]
                        br []
                    ]

                    tr [] [
                        td [] "World Size (slider in 10^x)"
                        let logSize = model.worldSize |> AVal.map (fun r -> Fun.Log10 r)
                        td [] (model.worldSize |> AVal.map (fun s -> System.String.Format("{0:0000000.00}", s)))
                        td [] [ slider { min = -2.0; max = 5.0; step = 0.01  } [ clazz "ui inverted red slider"; style "width: 100px"] logSize SetWorldSize ]
                    ]
                ]
            ]
        ]
    )

let threads (model : Model) = 
    ThreadPool.empty


let app =       
    let world = Box3d.Unit
    let freeFly, orbit = getInitial world 
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {   
                orbitCamera = orbit
                freeflyCamera = freeFly
                worldSize = world.Size.NormMax
                cameraSpeed = DefaultSpeedHeuristic(1.0, FreeFlyConfig.initial)
                mode = CameraMode.FreeFly
                autoAdjustSpeed = false
            }
        update = update 
        view = view
    }
