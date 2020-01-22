namespace Orbit

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives


type OrbitMessage =
    | MouseDown of V2i
    | MouseUp of V2i
    | MouseMove of V2i
    | Wheel of V2d


    | Rendered
    | SetTargetCenter of V3d
    | SetTargetPhi of float
    | SetTargetTheta of float
    | SetTargetRadius of float


[<ModelType>]
type OrbitState =
    {
        sky     : V3d
        center  : V3d
        phi     : float
        theta   : float
        radius  : float

        targetPhi : float
        targetTheta : float
        targetRadius : float
        targetCenter : V3d
        
        dragStart : Option<V2i>
        [<NonAdaptive>]
        lastRender : Option<MicroTime>

        view : CameraView
        
        radiusRange : Range1d
        thetaRange : Range1d
        moveSensitivity : float
        zoomSensitivity : float
        speed : float

    }

module OrbitState =

    let withView (s : OrbitState) =
        let l = V2d(s.phi, s.theta).CartesianFromSpherical() * s.radius + s.center
        { s with view = CameraView.lookAt l s.center s.sky }

    let create (center : V3d) (phi : float) (theta : float) (r : float) =
        let thetaRange = Range1d(-Constant.PiHalf + 0.0001, Constant.PiHalf - 0.0001)
        let radiusRange = Range1d(0.1, 40.0)

        let r = clamp radiusRange.Min radiusRange.Max r
        let theta = clamp thetaRange.Min thetaRange.Max theta

        withView {
            sky     = V3d.OOI
            center  = center
            phi     = phi
            theta   = theta
            radius  = r

            targetPhi = phi
            targetTheta = theta
            targetRadius = r
            targetCenter = center

            dragStart = None
            lastRender = None
            view = Unchecked.defaultof<_>

            radiusRange = radiusRange
            thetaRange = thetaRange
            moveSensitivity = 1.0
            zoomSensitivity = 1.0
            speed = 1.0
        }
