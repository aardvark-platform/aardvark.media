namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI

module Camera =

    let toFreeFlyState (speed : float) (o : OrbitState) =
        let cfg =
            { FreeFlyController.initial.freeFlyConfig with
                moveSensitivity = 0.35 + speed
                zoomMouseWheelSensitivity = 0.8 * (2.0 ** speed)
                panMouseSensitivity = 0.01 * (2.0 ** speed)
                dollyMouseSensitivity = 0.01 * (2.0 ** speed)
            }
        { 
            FreeFlyController.initial with 
                view = o.view 
                freeFlyConfig = cfg
        }
    
    let toOrbitState (radius : float) (newFreeFly : CameraControllerState) =
        let c = newFreeFly.view.Location + newFreeFly.view.Forward * radius
        let d = newFreeFly.view.Location - c |> Vec.normalize

        let theta = asin d.Z
        let xy = d.XY / sqrt (1.0 - d.Z)
        let phi = atan2 xy.Y xy.X

        OrbitState.create c phi theta radius


module OrbitState =

    let toFreeFly (speed : float) (v : OrbitState) = 
        Camera.toFreeFlyState speed v

    let ofFreeFly (radius : float) (v : CameraControllerState) = 
        Camera.toOrbitState radius v