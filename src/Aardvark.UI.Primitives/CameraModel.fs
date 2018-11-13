namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

[<DomainType>]
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
    }



[<DomainType>]
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
        moveVec     : V3i
        moveSpeed   : float
        panSpeed    : float
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
    