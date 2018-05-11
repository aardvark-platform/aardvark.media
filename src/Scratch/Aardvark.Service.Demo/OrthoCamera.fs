namespace OrthoCamera

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives
open OrthoCameraModel

module OrthoCameraDemo =

  type Action =    
      | CameraAction     of CameraController.Message
  
  let update (model : OrthoModel) (act : Action) =
    match act with
     | CameraAction a ->
       { model with camera = CameraController.update model.camera a }
  
  let view (model : MOrthoModel) =
      let cam =
          model.camera.view 
              
      let frustum =
          Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
    
      let box = Mod.constant (Box3d(-V3d.III, V3d.III))
      let c = Mod.constant (C4b.DarkCyan)
  
      let scene = 
        Sg.box c box                            
          |> Sg.shader {
              do! DefaultSurfaces.trafo
              do! DefaultSurfaces.vertexColor
              do! DefaultSurfaces.simpleLighting
              }    
      
      let renderControlAttributes = CameraController.extractAttributes model.camera CameraAction frustum |> AttributeMap.ofAMap
  
      let orthoTrafo =
        let d = 5.0
        
        let min = V3d(-d, -d, -d*2.0)
        let max = V3d(d, d, d*2.0)
        let fr = Frustum.ortho (Box3d(min, max))
        Mod.constant (Frustum.orthoTrafo fr)

      let ortho =
        let d = 5.0
        
        let min = V3d(-d, -d, -d*2.0)
        let max = V3d(d, d, d*2.0)
        let fr = Frustum.ortho (Box3d(min, max))
        Mod.constant (fr)

      require (Html.semui) ( 
          Incremental.renderControl'
            (Mod.map2 Camera.create model.camera.view ortho)
            (AttributeMap.unionMany [
                renderControlAttributes                                
                [
                  attribute "style" "width:100%; height: 100%; float: left;"
                //  //onKeyDown (KeyDown)
                //  //onKeyUp (KeyUp) 
                ] |> AttributeMap.ofList
            ])            
            scene
      )
  
  let threads m = 
    CameraController.threads m.camera |> ThreadPool.map CameraAction
  
  let initialView = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
      unpersist = Unpersist.instance     
      threads = threads
      initial = { camera = { CameraController.initial with view = initialView }}
      update = update 
      view = view
    }