namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI

module Camera =

    let defaultConfig (speed : float) = 
        let cfg =
            { FreeFlyController.initial.freeFlyConfig with
                moveSensitivity = 0.35 + speed
                zoomMouseWheelSensitivity = 0.8 * (2.0 ** speed)
                panMouseSensitivity = 0.01 * (2.0 ** speed)
                dollyMouseSensitivity = 0.01 * (2.0 ** speed)
            }
        cfg

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
                view = o._view 
                freeFlyConfig = cfg
        }
    
    let toOrbitState (radius : float) (newFreeFly : CameraControllerState) =
        let view = newFreeFly.view
        let forward = view.Forward.Normalized // - c |> Vec.normalize
        let center = view.Location + forward * radius
        let sky = newFreeFly.view.Sky
        let right = newFreeFly.view.Right

        let basis = M44d.FromBasis(right, Vec.cross sky right, sky, V3d.Zero)

        let sphereForward = basis.TransposedTransformDir -forward
        let phiTheta = sphereForward.SphericalFromCartesian()

        OrbitState.create' newFreeFly.view.Right newFreeFly.view.Sky center phiTheta.X phiTheta.Y radius

[<AutoOpen>]
module Extensions =

    module OrbitState =
        let toFreeFly (speed : float) (v : OrbitState) = 
            Camera.toFreeFlyState speed v

        let ofFreeFly (radius : float) (v : CameraControllerState) = 
            Camera.toOrbitState radius v

        let view_ = OrbitState._view_

    type OrbitState with
        member x.radius = x._radius
        member x.view = x._view





