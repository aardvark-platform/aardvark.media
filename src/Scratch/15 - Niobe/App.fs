namespace Niobe

module App = 
  open Aardvark.UI
  open Aardvark.UI.Primitives
  
  open Aardvark.Base
  open Aardvark.Base.Incremental
  open Aardvark.Base.Rendering
  
  
  let update (model : Model) (msg : Message) =
      match msg with        
        | Camera m ->
          { model with cameraState = FreeFlyController.update model.cameraState m; }
        | UpdateDockConfig cfg ->
          { model with dockConfig = cfg }
        | Message.KeyDown k -> model
        | Message.KeyUp k -> model
        //| _ -> model 
  
  let view (m : MModel) =
                                                 
      let box = 
        Sg.box (Mod.constant C4b.Red) (Mod.constant Box3d.Unit)
          |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

      let scene = 
        [
          box
          //PickingApp.view m.picking
        ] |> Sg.ofList

      let renderControl =
       FreeFlyController.controlledControl m.cameraState Camera (Frustum.perspective 60.0 0.01 1000.0 1.0 |> Mod.constant) 
         (AttributeMap.ofList [ 
           style "width: 100%; height:100%"; 
           attribute "showFPS" "false";       // optional, default is false
           attribute "useMapping" "true"
           attribute "data-renderalways" "false"
           attribute "data-samples" "4"
           onKeyDown (Message.KeyDown)
           onKeyUp (Message.KeyUp)
           //onBlur (fun _ -> Camera FreeFlyController.Message.Blur)
         ]) 
         (scene) 
                  
      page (fun request -> 
        match Map.tryFind "page" request.queryParams with
        | Some "render" ->
          require Html.semui ( // we use semantic ui for our gui. the require function loads semui stuff such as stylesheets and scripts
              div [clazz "ui"; style "background: #1B1C1E"] [renderControl]
          )
        | Some "controls" -> 
          require Html.semui (
            body [style "width: 100%; height:100%; background: transparent";] [
               div[style "color:white"][text "UI COMES HERE"]
            ]
          )
        | Some other -> 
          let msg = sprintf "Unknown page: %A" other
          body [] [
              div [style "color: white; font-size: large; background-color: red; width: 100%; height: 100%"] [text msg]
          ]  
        | None -> 
          m.dockConfig
            |> docking [
              style "width:100%; height:100%; background:#F00"
              onLayoutChanged UpdateDockConfig ]
        )
  
  
  let threads (model : Model) = 
      ThreadPool.empty
  
  let initialCamera = { FreeFlyController.initial with view = CameraView.lookAt (V3d.III * 2.0) V3d.OOO V3d.OOI; }

  let initialModel : Model = 
    { 
      cameraState        = initialCamera                                
      threads            = FreeFlyController.threads initialCamera |> ThreadPool.map Camera                            
      dockConfig         =
        config {
            content (
                horizontal 10.0 [
                    element { id "render"; title "Render View"; weight 7.0 }
                    element { id "controls"; title "Controls"; weight 3.0 }                                                    
                ]
            )
            appName "OpcSelectionViewer"
            useCachedConfig true
        }
    }
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
      {
          unpersist = Unpersist.instance     
          threads = threads 
          initial = initialModel
          update = update 
          view = view
      }
