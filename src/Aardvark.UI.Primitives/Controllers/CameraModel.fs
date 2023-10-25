namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Application
open Adaptify

[<ModelType>]
type FreeFlyConfig = 
    {
        lookAtMouseSensitivity  : float
        lookAtConstant          : float
        lookAtDamping           : float
        
        jumpAtConstant          : float
        jumpAtDamping           : float

        panMouseSensitivity     : float
        panConstant             : float
        panDamping              : float
        
        dollyMouseSensitivity   : float
        dollyConstant           : float
        dollyDamping             : float

        zoomMouseWheelSensitivity : float
        zoomConstant              : float
        zoomDamping                : float

        moveSensitivity : float

        touchScalesExponentially : bool
    }

module FreeFlyConfig =
    let initial = {
        lookAtMouseSensitivity       = 0.01
        lookAtConstant               = 0.1
        lookAtDamping                = 30.0
                                     
        jumpAtConstant               = 0.025
        jumpAtDamping                = 7.5

        panMouseSensitivity          = 0.05
        panConstant                  = 0.01
        panDamping                   = 3.0
                                     
        dollyMouseSensitivity        = 0.175
        dollyConstant                = 0.05
        dollyDamping                 = 3.25
                                     
        zoomMouseWheelSensitivity    = 1.5
        zoomConstant                 = 0.05
        zoomDamping                  = 3.25

        moveSensitivity = 1.0

        touchScalesExponentially = true
    }


module FreeFlyHeuristics =

    type SpeedHeuristic =
        abstract member Config : FreeFlyConfig
        abstract member SpeedCoefficient : float
        abstract member AdjustToUnitsPerSecond : float -> SpeedHeuristic
        abstract member AdjustSpeedCoefficient : float -> SpeedHeuristic

    type DefaultSpeedHeuristic(speedCoefficient : float, config : FreeFlyConfig) = 

        // must be inverse.
        let cameraSpeedToMoveSensitivity (speed : float) =  0.35 + speed
        let cameraSpeedFromMoveSensitivity (sensitivity : float) = sensitivity - 0.35

        member x.AdjustSpeedCoefficient(speedCoefficient : float) =
            let config =
                { config with
                    moveSensitivity = cameraSpeedToMoveSensitivity speedCoefficient
                    zoomMouseWheelSensitivity = 0.8 * (2.0 ** speedCoefficient)
                    panMouseSensitivity = 0.01 * (2.0 ** speedCoefficient)
                    dollyMouseSensitivity = 0.01 * (2.0 ** speedCoefficient)
                }
            DefaultSpeedHeuristic(speedCoefficient, config) :> SpeedHeuristic

        member x.AdjustToUnitsPerSecond(velocity : float) =
            let exponent = log velocity
            let coefficient = cameraSpeedFromMoveSensitivity exponent
            x.AdjustSpeedCoefficient(coefficient)

        member x.SpeedCoefficient = speedCoefficient
        member x.Config = config

        interface SpeedHeuristic with
            member x.Config = x.Config
            member x.SpeedCoefficient = x.SpeedCoefficient
            member x.AdjustToUnitsPerSecond(v) = x.AdjustToUnitsPerSecond(v)
            member x.AdjustSpeedCoefficient(v) = x.AdjustSpeedCoefficient(v)



[<ModelType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        movePos   : V2i
        look      : bool
        zoom      : bool
        pan       : bool
        dolly     : bool
        isWheel     : bool
        scrolling   : bool
        
        forward     : bool
        backward    : bool
        left        : bool
        right       : bool
        upward      : bool
        downward    : bool

        moveVec     : V3d
        rotateVec   : V3d

        moveSpeed   : float
        panSpeed    : float

        upSpeed     : float
        downSpeed   : float

        orbitCenter : Option<V3d>
        lastTime    : Option<float>

        animating   : bool

        sensitivity       : float
        scrollSensitivity : float
        zoomFactor        : float
        panFactor         : float
        rotationFactor    : float    
        
        targetPhiTheta  : V2d
        targetPan : V2d    
        targetDolly : float
        targetZoom : float
        
        targetJump : Option<CameraView>

        freeFlyConfig : FreeFlyConfig

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 
    }
    



type OrbitControllerConfig = 
    {
        isPan : MouseButtons -> bool
    }

[<ModelType>]
type OrbitState =
    internal {
        sky     : V3d
        right   : V3d

        center  : V3d
        phi     : float
        theta   : float
        _radius  : float

        targetPhi : float
        targetTheta : float
        targetRadius : float
        targetCenter : V3d

        
        dragStart : Option<V2i>
        panning   : bool
        pan       : V2d
        targetPan : V2d

        [<NonAdaptive>]
        lastRender : Option<MicroTime>

        _view : CameraView
        
        radiusRange : Range1d
        thetaRange : Range1d
        moveSensitivity : float
        zoomSensitivity : float
        speed : float

        config : OrbitControllerConfig
    } 

