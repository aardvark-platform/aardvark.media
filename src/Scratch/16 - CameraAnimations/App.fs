namespace CameraAnimations

module CameraAnimations = 
  open System
  
  open Aardvark.UI
  open Aardvark.UI.Primitives
  open Aardvark.UI.Animation
  
  open Aardvark.Base
  open Aardvark.Base.Incremental
  open Aardvark.Base.Rendering
  
  let oneIteration (pos:V3d) (dir : V3d) (state : CameraView) (localTime : RelativeTime) (duration : RelativeTime) =
    let vec = pos - state.Location
    let dist = vec.Length
    let rot      = Rot3d(state.Forward, dir) * localTime / duration
    let forward' = rot.TransformDir(state.Forward)
    
    let velocity  = dist / duration
    let dir       = vec.Normalized
    let location' = state.Location + dir * velocity * localTime
        
    let view = 
      state 
        |> CameraView.withForward forward'
        |> CameraView.withLocation location'
    
    Some (state,view)

  let animateFowardAndLocation (pos : V3d) (dir : V3d) (duration : RelativeTime) (name : string) = 
    {
      (CameraAnimations.initial name) with 
        sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview

          if localTime < duration then
            oneIteration pos dir state localTime duration
          else             
            let view = 
              state 
                |> CameraView.withForward dir
                |> CameraView.withLocation pos
    
            Some (state,view)        
    }

  let _view = Model.Lens.cameraState |. CameraControllerState.Lens.view

  let update (model : Model) (msg : Message) =
    match msg with        
      | Camera m ->
          { model with cameraState = FreeFlyController.update model.cameraState m; }
      | AnimationMessage m ->
        let a = AnimationApp.update model.animations m
        { model with animations = a } |> Lenses.set _view a.cam //don't forget to sync the camstate
      | UpdateDockConfig cfg ->
        { model with dockConfig = cfg }
      | Message.KeyDown k -> model
      | Message.KeyUp k -> model
      | FlyTo ->
        let animationMessage = animateFowardAndLocation (V3d.One * 3.0) model.cameraState.view.Forward 2.0 "ForwardAndLocation2s"
        Log.line "enqueued animation %A" animationMessage.name
        let anim = { model.animations with cam = model.cameraState.view }
        let anim = AnimationApp.update anim (AnimationAction.PushAnimation(animationMessage))
        { model with animations = anim }
      | Reset ->
        { model with cameraState = Model.initialCamera }
  
  let textOverlays (cv : IMod<CameraView>) = 
    let position = cv |> Mod.map(fun x -> x.Location.ToString("0.0000"))
    let style' = "color: white; font-family:Consolas;"

    div [js "oncontextmenu" "event.preventDefault();"; clazz "ui"; style "position: absolute; top: 15px; left: 15px; float:left"] [
      table [] [
        tr[][
          td[style style'][text "Position: "]
          td[style style'][Incremental.text position]
        ]
      ]
    ]

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
            body [style "width: 100%; height:100%; background: transparent";] [
              div [clazz "ui"; style "background: #1B1C1E"] [
                renderControl
                textOverlays m.cameraState.view
              ]
            ]
          )
        | Some "controls" -> 
          require Html.semui (
            body [style "width: 100%; height:100%; background: transparent";] [
               div[style "color:white"][
                 button[clazz "ui button"; onClick (fun _ -> FlyTo)][text "fly to cube"]
                 button[clazz "ui button"; onClick (fun _ -> Reset)][text "reset"]
               ]
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
  
  
  let threads (model : Model) = AnimationApp.ThreadPool.threads model.animations |> ThreadPool.map AnimationMessage
  
  
  
  let initialModel : Model = 
    { 
      cameraState        = Model.initialCamera                                
      threads            = FreeFlyController.threads Model.initialCamera |> ThreadPool.map Camera
      dockConfig         =
        config {
            content (
                horizontal 10.0 [
                    element { id "render"; title "Render View"; weight 7.0 }
                    element { id "controls"; title "Controls"; weight 3.0 }                                                    
                ]
            )
            appName "Animation sandbox"
            useCachedConfig true
        }
      animations = AnimationModel.initial
    }
  
  let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
      {
          unpersist = Unpersist.instance     
          threads = threads 
          initial = initialModel
          update = update 
          view = view
      }
