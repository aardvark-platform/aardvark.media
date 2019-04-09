namespace Model

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives
open System
open Aardvark.Base
open Aardvark.Base

type Message = 
    | Camera            of FreeFlyController.Message
    | CenterScene
    | ChangePan         of Numeric.Action
    | ChangeTilt        of Numeric.Action
    | ChangeZoom        of Numeric.Action
    | ChangePosition    of Vector3d.Action
    | UpdateDockConfig  of DockConfig 

[<DomainType>]
type Model = 
    {
        cameraMain     : CameraControllerState
        camera2        : CameraControllerState
        pan            : NumericInput
        tilt           : NumericInput
        zoom           : NumericInput
        position       : V3dInput
        lookAt         : V3d
        up             : V3d
        frustumCam2    : Frustum
        dockConfig     : DockConfig
    }

module Init =
    let pan = 
      {
        min    = 0.0
        max    = 360.0
        step   = 1.0
        format = "{0:0.0}"
        value  = 180.0
      }

    let tilt = 
      {
        min    = 0.0
        max    = 360.0
        step   = 1.0
        format = "{0:0.0}"
        value  = 180.0
      }

    let zoom = 
      {
        min    = 0.0
        max    = 20.0
        step   = 1.0
        format = "{0:0.0}"
        value  = 0.0
      }

    let positionInput = {
            value   = 0.0
            min     = Double.MinValue
            max     = Double.MaxValue
            step    = 0.01
            format  = "{0:0.00}"
        }

    let position (v : V3d) = {
            x = { positionInput with value = v.X }
            y = { positionInput with value = v.Y }
            z = { positionInput with value = v.Z }
            value = v    
        }

    let initialCamera = { 
        FreeFlyController.initial' 3.0 with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

    let initialCamera2 = { 
        FreeFlyController.initial' 3.0 with 
            view = CameraView.lookAt (V3d(0.0, 2.0, 1.0)) V3d.OOO V3d.OOI
    }

    // WACL
    //<FocalLengthPx>1769.2, 1769.2</FocalLengthPx>
    //<!-- angle (gon) for camera horizontal FoV-->
    //<HorizontalFoV>35.87</HorizontalFoV>
    //<Resolution>1024, 1024</Resolution>
    let ifrustum = Frustum.perspective ((35.87).DegreesFromGons()) 0.001 100.0 1.0

    let initialModel : Model = 
        { 
            cameraMain     = initialCamera
            camera2        = initialCamera2
            pan            = pan
            tilt           = tilt
            zoom           = zoom
            position       = position (V3d(0.0, 2.0, 1.0))                       
            lookAt         = initialCamera2.view.Forward
            up             = initialCamera2.view.Up
            frustumCam2    = ifrustum
            dockConfig     =
                config {
                    content (
                        horizontal 10.0 [
                            element { id "render"; title "MainView"; weight 7.0 }
                            element { id "2ndCam"; title "2ndView"; weight 5.0 }
                            element { id "controls"; title "Controls"; weight 4.0 }                                                    
                        ]
                    )
                    appName "FootPrintViewer"
                    useCachedConfig true
                }
        }