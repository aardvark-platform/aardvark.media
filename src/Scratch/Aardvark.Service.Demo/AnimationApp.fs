namespace AnimationDemo

  module AnimationDemoApp = 
    
    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Incremental.Operators
    open Aardvark.Base.Rendering
    
    open Aardvark.UI
    open Aardvark.UI.Primitives
    open Aardvark.UI.Animation
                
    // TODO: head tail patterns for plist
    // update at for plist
    module Lens =
      let update (l : Lens<'m,'a>) (s : 'm) (f : 'a -> 'a) =
        l.Update(s,f)
                
    let update (m : DemoModel) (msg : Message ) =
        match msg with
            | CameraMessage msg when not (AnimationApp.shouldAnimate m.animations) -> 
              let cc = CameraController.update m.cameraState msg               
              { m with cameraState = cc; animations = { m.animations with cam = cc.view}}
            | CameraMessage _ -> m // not allowed to camera around           
            | AnimationMessage msg ->
              let a = AnimationApp.update m.animations msg
              { m with animations = a; cameraState = { m.cameraState with view = a.cam}}
    
    let viewScene (m : MDemoModel) =
        let b1 =
          Sg.box ~~C4b.DarkRed ~~Box3d.Unit
          |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
           }

        let b2 = 
          Sg.box ~~C4b.DarkGreen ~~Box3d.Unit
          |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
           }
          |> Sg.trafo (Mod.constant (Trafo3d.Translation(V3d.IOO * -2.0)))

        Sg.ofList [b1; b2]
        
    let view (m : MDemoModel) =            
      body [ style "background: #1B1C1E"] [
        require (Html.semui) (
          div [clazz "ui"; style "background: #1B1C1E"] [
            CameraController.controlledControl m.cameraState CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                (AttributeMap.ofList [ attribute "style" "width:85%; height: 100%; float: left;"]) (viewScene m)
    
            div [style "width:15%; height: 100%; float:right"] [
              Html.SemUi.stuffStack [
                b [] [text "Camera actions:"]; br[]; 
                
                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.zoom  1.0 "zoom in  (1s)"))] [text "Zoom in"] 
                    |> UI.map AnimationMessage

                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.zoom -1.0 "zoom out (1s)"))] [text "Zoom away"] 
                    |> UI.map AnimationMessage

                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.animateLocation (V3d.IOI * 3.0) 2.0 "flyto 333"))] [text "FlyTo in 2s"] 
                    |> UI.map AnimationMessage

                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.animateLocationFixedLookAt (V3d.IOI * 3.0) (V3d.Zero) 2.0 "flyto fixed look"))] [text "FlyTo fixed in 2s"] 
                    |> UI.map AnimationMessage

                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.animateLookAt (V3d.Zero) (V3d.IOO * -2.0) 2.0 "lookAt"))] [text "rotate"] 
                    |> UI.map AnimationMessage

                button [clazz "ui button"; onClick (fun _ ->  
                  AnimationAction.PushAnimation (CameraAnimations.animateFoward ((V3d.IOO * -2.0) - (m.cameraState.view |> Mod.force).Location).Normalized 2.0 "rotate"))] [text "foward"] 
                    |> UI.map AnimationMessage
    
                br[]; br[]; b [] [text "Pending animations (click to abort)"]
                Incremental.div AttributeMap.empty <| AList.mapi (fun i a ->
                    button [onClick (fun _ -> AnimationAction.RemoveAnimation i)] [text a.name] |> UI.map AnimationMessage
                ) m.animations.animations
              ]
            ]
          ]
        )
      ]
    
    
    module ThreadPool =
      let unionMany xs = List.fold ThreadPool.union ThreadPool.empty xs
    
      let threads (m : DemoModel) = 
        // handling of continous camera animations (camera controller)
        let cameraAnimations = CameraController.threads m.cameraState |> ThreadPool.map CameraMessage
                
        let animations = AnimationApp.ThreadPool.threads m.animations |> ThreadPool.map AnimationMessage
           
        // combining all threads
        unionMany [
          cameraAnimations
          animations                        
        ]
    
    let initialView = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
    
    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
      {
        unpersist = Unpersist.instance     
        threads = ThreadPool.threads
        initial = 
          { 
             cameraState =  
               { CameraController.initial with 
                    view = initialView
               }
             animations = 
               { 
                 animations =  PList.empty; 
                 animation  = Animate.On; 
                 cam        = initialView }
               }
        update = update 
        view = view
      }
