module App

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model
open Utils
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.SceneGraph


let update2ndCam (model : Model) =
    let right = - model.up.Cross(model.lookAt)
    let rotPanFw = Rot3d.Rotation(model.up, model.pan.value.RadiansFromDegrees()).Transform(model.lookAt )

    let lookAt = Rot3d.Rotation(right, model.tilt.value.RadiansFromDegrees()).Transform(rotPanFw )
    let up = Rot3d.Rotation(right, model.tilt.value.RadiansFromDegrees()).Transform(model.up)

    let view = CameraView.look model.position.value lookAt.Normalized up.Normalized 
    match model.textureActive with
     | true -> { model with camera2 = { model.camera2 with view = view };
                             textureProj = {model.textureProj with cam = { model.textureProj.cam with view = view}}}
     | false -> { model with camera2 = { model.camera2 with view = view };
                            footprintProj = {model.footprintProj with cam = { model.footprintProj.cam with view = view}}}
   

let getFrustum (model : AdaptiveModel) =
    adaptive {
        let! texActive = model.textureActive
        let! tpf = model.textureProj.frustum
        let! fppf = model.footprintProj.frustum 
        match texActive with
            | true -> return tpf
            | false -> return fppf
        
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraMain = FreeFlyController.update model.cameraMain m }
        | CenterScene -> 
            { model with cameraMain = Init.initialCamera }
        | ChangePan p -> 
            let m' = { model with pan = Numeric.update model.pan p }
            update2ndCam m'
        | ChangeTilt t -> 
            let m' = { model with tilt = Numeric.update model.tilt t }
            update2ndCam m'
        | ChangeZoom z -> 
            let m' = { model with zoom = Numeric.update model.zoom z }
            m'
        | ChangePosition p ->
            let p' = Vector3d.update model.position p
            let m' = { model with position =  p' }
            update2ndCam m'
        | UpdateDockConfig cfg ->
            { model with dockConfig = cfg }
        | Message.KeyDown k ->
            let position' =  Vector3d.updateV3d model.position model.cameraMain.view.Location
            let cam2 = { model.camera2 with view = model.cameraMain.view }
            match k with 
                | Aardvark.Application.Keys.F1 -> 
                    { model with camera2 = cam2
                                 footprintProj = {model.footprintProj with cam = {model.footprintProj.cam with view = model.cameraMain.view}}; 
                                 position = position';
                                 textureActive = false}
                | Aardvark.Application.Keys.F2 ->
                    { model with camera2 = cam2
                                 textureProj = {model.textureProj with cam = {model.textureProj.cam with view = model.cameraMain.view}}; 
                                 position = position';
                                 textureActive = true}
                | _ -> model



let view (model : AdaptiveModel) =

    let viewV3dInput (model : AdaptiveV3dInput) =  
            Html.table [                            
                Html.row "X" [Numeric.view' [InputBox] model.x |> UI.map Vector3d.Action.SetX]
                Html.row "Y" [Numeric.view' [InputBox] model.y |> UI.map Vector3d.Action.SetY]
                Html.row "Z" [Numeric.view' [InputBox] model.z |> UI.map Vector3d.Action.SetZ]
            ]       

    let renderControl =
      FreeFlyController.controlledControl model.cameraMain Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
        (AttributeMap.ofList [ 
            attribute "showFPS" "true"; 
            attribute "data-renderalways" "1"; 
            attribute "data-samples" "8"; 
            style "width: 100%; height:80%";
            onKeyDown (Message.KeyDown)])
        (Scene.fullScene model)
    
    // WACL
    //<Resolution>1024, 1024</Resolution>
    let height = "height:" + (1024/2).ToString() + ";" ///uint32(2)
    let width = "width:" + (1024/2).ToString() + ";" ///uint32(2)

    let renderControl2ndCam =
      FreeFlyController.controlledControl model.camera2 Camera (getFrustum model) 
        (AttributeMap.ofList [ 
            attribute "showFPS" "true"; 
            attribute "data-renderalways" "1"; 
            attribute "data-samples" "8"; 
            style ("background: #1B1C1E;" + height + width)
            onKeyDown (Message.KeyDown)])
        (Scene.camScene model)

    page (fun request -> 
        match Map.tryFind "page" request.queryParams with
        | Some "render" ->
          require Html.semui ( 
              div [clazz "ui"; style "background: #1B1C1E"] [renderControl]
          )
        | Some "2ndCam" ->
          require Html.semui ( 
              div [clazz "ui"; style "background: #1B1C1E"] [renderControl2ndCam]
          )
        | Some "controls" -> 
          require Html.semui (
            body [style "width: 100%; height:100%; background: transparent";] [
               div[style "color:white"] [
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene.."]
                    div[style "width:400px"] [
                        Html.table [
                          Html.row "Pan:" [Numeric.view' [NumericInputType.Slider] model.pan |> UI.map ChangePan ]  
                          Html.row "Tilt:" [Numeric.view' [NumericInputType.Slider] model.tilt |> UI.map ChangeTilt ] 
                          //Html.row "Zoom:" [Numeric.view' [NumericInputType.Slider] model.zoom |> UI.map ChangeZoom ] 
                          Html.row "Position:" [viewV3dInput model.position |> UI.map ChangePosition ]
                        ]
                      ]
               ]
            ]
          )
        | Some other -> 
          let msg = sprintf "Unknown page: %A" other
          body [] [
              div [style "color: white; font-size: large; background-color: red; width: 100%; height: 100%"] [text msg]
          ]  
        | None -> 
          model.dockConfig
            |> docking [
              style "width:100%; height:100%; background:#F00"
              onLayoutChanged UpdateDockConfig ]
        )
        

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = fun _ -> ThreadPool.empty  
        initial = Init.initialModel
        update = update 
        view = view
    }
