namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application


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
        
        forward     : bool
        backward    : bool
        left        : bool
        right       : bool
        moveVec     : V3i
        moveSpeed   : float
        panSpeed    : float
        orbitCenter : Option<V3d>
        lastTime    : Option<float>
        isWheel     : bool

        animating   : bool

        sensitivity       : float
        scrollSensitivity : float
        scrolling         : bool
        zoomFactor        : float
        panFactor         : float
        rotationFactor    : float    
        
        targetPhiTheta  : V2d
        targetPan : V2d    
        targetDolly : float

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 
    }

