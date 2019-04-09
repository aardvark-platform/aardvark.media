module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model
open Utils
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.SceneGraph


let update2ndCam (model : Model) =
    let right = - model.up.Cross(model.lookAt)
    let rotPanFw = Rot3d(model.up, model.pan.value.RadiansFromDegrees()).TransformDir(model.lookAt )

    let lookAt = Rot3d(right, model.tilt.value.RadiansFromDegrees()).TransformDir(rotPanFw )
    let up = Rot3d(right, model.tilt.value.RadiansFromDegrees()).TransformDir(model.up)

    let view = CameraView.look model.position.value lookAt.Normalized up.Normalized 
    { model with camera2 = { model.camera2 with view = view } }

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



let view (model : MModel) =

    let viewV3dInput (model : MV3dInput) =  
            Html.table [                            
                Html.row "X" [Numeric.view' [InputBox] model.x |> UI.map Vector3d.Action.SetX]
                Html.row "Y" [Numeric.view' [InputBox] model.y |> UI.map Vector3d.Action.SetY]
                Html.row "Z" [Numeric.view' [InputBox] model.z |> UI.map Vector3d.Action.SetZ]
            ]       

    let renderControl =
      FreeFlyController.controlledControl model.cameraMain Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
        (AttributeMap.ofList [ 
            attribute "showFPS" "true"; 
            attribute "data-renderalways" "1"; 
            attribute "data-samples" "8"; 
            style "width: 100%; height:80%"])
        (Scene.fullScene model)
    
    // WACL
    //<Resolution>1024, 1024</Resolution>
    let height = "height:" + (1024/2).ToString() + ";" ///uint32(2)
    let width = "width:" + (1024/2).ToString() + ";" ///uint32(2)

    let renderControl2ndCam =
      FreeFlyController.controlledControl model.camera2 Camera (model.frustumCam2) 
        (AttributeMap.ofList [ 
            attribute "showFPS" "true"; 
            attribute "data-renderalways" "1"; 
            attribute "data-samples" "8"; 
            style ("background: #1B1C1E;" + height + width)])
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
               div[style "color:white"][
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene.."]
                    div[style "width:400px"][
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
