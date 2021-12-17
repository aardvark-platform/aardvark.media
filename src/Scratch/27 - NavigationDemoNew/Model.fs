namespace Inc.Model

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.FreeFlyHeuristics
open Adaptify

type CameraMode = 
    | FreeFly = 0
    | Orbit = 1

type Message = 
    | FreeFlyCameraMessage of FreeFlyController.Message
    | OrbitCameraMessage   of OrbitMessage

    | SetOrbitCenter     of V3d
    | ResetCamera
    | SetCameraCoefficient  of float
    | AdjustMoveVelocity    of float
    | ToggleAutoAdjustSpeed

    | IncreaseMoveSpeed
    | DecreaseMoveSpeed

    | SetMode of CameraMode
    | SetWorldSize of float



[<ModelType>]
type Model = 
    {
        freeflyCamera : CameraControllerState
        orbitCamera   : OrbitState
        mode          : CameraMode
        cameraSpeed   : SpeedHeuristic

        autoAdjustSpeed : bool
        worldSize : float
    }