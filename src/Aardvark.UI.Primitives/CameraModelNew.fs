namespace Aardvark.UI.Primitives.CameraControllers.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental


[<DomainType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        movePos   : V2i
        look      : bool
        zoom      : bool
        pan       : bool

        pendingMove : V3d

        forward     : Option<TimeSpan>
        backward    : Option<TimeSpan>
        left        : Option<TimeSpan>
        right       : Option<TimeSpan>
        moveVec     : V3i
        moveSpeed   : float
        orbitCenter : Option<V3d>
        lastTime    : Option<float>
        isWheel     : bool

        sensitivity     : float
        scrollSensitivity : float
        scrolling : bool
        zoomFactor      : float
        panFactor       : float
        rotationFactor  : float        

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 
    }

