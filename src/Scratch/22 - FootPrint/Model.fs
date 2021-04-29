namespace Model

open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open System
open Adaptify



type Message = 
    | Camera            of FreeFlyController.Message
    | CenterScene
    | ChangePan         of Numeric.Action
    | ChangeTilt        of Numeric.Action
    | ChangeZoom        of Numeric.Action
    | ChangePosition    of Vector3d.Action
    | UpdateDockConfig  of DockConfig 
    | KeyDown           of key : Keys  

[<ModelType>]
type ProjectionModel = 
    {
        frustum         : Frustum
        cam             : CameraControllerState
    }

[<ModelType>]
type Model = 
    {
        cameraMain          : CameraControllerState
        camera2             : CameraControllerState
        pan                 : NumericInput
        tilt                : NumericInput
        zoom                : NumericInput
        position            : V3dInput
        lookAt              : V3d
        up                  : V3d
        textureProj         : ProjectionModel
        footprintProj       : ProjectionModel
        textureActive       : bool
        dockConfig          : DockConfig
    }

module Init =
    //let bigVal value = value + 3000000.0
    //let bigVec (value:V3d) = value + 3000000.0

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
    let frustumFP = Frustum.perspective ((35.87).DegreesFromGons()) 0.001 100.0 1.0

    let initialCameraFP = { 
        FreeFlyController.initial' 3.0 with 
            view = CameraView.lookAt (V3d(0.0, 2.0, 1.0)) V3d.OOO V3d.OOI
    }

    let projectionModelFP = {
        frustum = frustumFP
        cam     = initialCameraFP
    }

    //<HRC_PanCam>
    //<!-- focal length in mm (PRo3D: as user information) -->
    //                <FocalLength>180</FocalLength>
    //<Resolution>1024, 1024</Resolution>
    // <!-- angle (gon) for camera horizontal FoV-->
    //<HorizontalFoV>5.42</HorizontalFoV>
    
    let frustumTex = Frustum.perspective ((35.87).DegreesFromGons()) 0.001 100.0 1.0

    let initialCameraTex = { 
        FreeFlyController.initial' 3.0 with 
            view = CameraView.lookAt (V3d(0.0, -2.0, 1.0)) V3d.OOO V3d.OOI
    }

    let projectionModelTex = {
        frustum = frustumTex
        cam     = initialCameraTex
    }

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
            //frustumCam2    = ifrustum
            textureProj    = projectionModelFP
            footprintProj  = projectionModelTex
            textureActive  = false
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