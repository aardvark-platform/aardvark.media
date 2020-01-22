namespace OrthoCamera

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives


module OrthoCameraDemo =

    type Action =    
        | CameraAction of FreeFlyController.Message
        | HitSurface   of V3d

    let orthoFrustumFromWidth (width : float) =      
        let halfwidth = width / 2.0      
        let min = new V3d(-halfwidth, -halfwidth, -50.0)
        let max = new V3d( halfwidth,  halfwidth,  50.0)
        Frustum.ortho (Box3d(min, max))

    let update (model : OrthoModel) (msg : Action) =
        match msg with
        | CameraAction a ->
            { model with cameraState = FreeFlyController.update model.cameraState a }
        | HitSurface p -> 
            Log.line "hitted %A" p
            { model with point = p }
    
    let frustum = 
        orthoFrustumFromWidth 5.0
        //Frustum.perspective 60.0 0.1 100.0 1.0             

    let view (model : MOrthoModel) =

        let sg =
            Sg.box (AVal.constant C4b.White) (AVal.constant (Box3d(-V3d.III, V3d.III)))                        
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }        
            |> Sg.requirePicking
            |> Sg.noEvents            
            |> Sg.withEvents [
                Sg.onDoubleClick (fun p -> HitSurface p) 
            ]

        let s = 
            Sg.sphere 4 (AVal.constant C4b.Red) (AVal.constant 0.15)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
                }
            |> Sg.noEvents
            |> Sg.trafo (model.point |> AVal.map(Trafo3d.Translation))
            

        let attributes = 
          [            
            (attribute "style" "width:100%; height: 100%; background-color: #444444")
            (attribute "useMapping" "true")
            (attribute "data-samples" "4")
         //   (onBlur (fun _ -> CameraAction FreeFlyController.Message.Blur))
            //(onEvent "onRendered" [] (fun _ -> CameraAction FreeFlyController.Message.Rendered))
          ] |> AttributeMap.ofList

        let attributes = AttributeMap.union attributes (FreeFlyController.attributes model.cameraState CameraAction)

        let cam = AVal.map2 Camera.create model.cameraState.view (frustum |> AVal.constant)

        body [] [
            //FreeFlyController.controlledControl model.camera Action.CameraAction frustum (AttributeMap.ofList att) sg
            Incremental.renderControlWithClientValues' cam attributes RenderControlConfig.standard ([sg; s] |> Sg.ofList |> constF)
        ]    
    
    let threads (model : OrthoModel) = 
        FreeFlyController.threads model.cameraState |> ThreadPool.map CameraAction
        
    let camState = FreeFlyController.initial    

    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = { cameraState = camState; point = V3d.Zero }
              
            update = update
            view = view
        }
