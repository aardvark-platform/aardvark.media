module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model


let initialCamera = { 
        CameraController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = CameraController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let dockingBoot = """
var init = function(element,id,info) {
    element.innerHTML = "<iframe src='./?page=" + id + "' style='border:none;width:100%;height:100%;'></iframe>";
};   

var config =
    {
        content : {
            kind: 'horizontal',
            weight: 10,
            children : [
                { kind : 'element', id : 'render', weight: 20 },
                { kind : 'element', id : 'button', weight: 5 }
            ]
        }
    };


var layouter = new Docking.DockLayout(document.getElementById('__ID__'), config, init);

"""

let view (model : MModel) =

    let docking = [
        { name = "docking-js-style"; url = "http://tatooine.awx.at/docking-js/docking.css"; kind = Stylesheet }
        { name = "docking-js"; url = "http://tatooine.awx.at/docking-js/docking.js"; kind = Script }
    ]

    let renderControl =
        CameraController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 100%; height:100%;"]) 
                    (viewScene model)

    page (fun request -> 
        match Map.tryFind "page" request.queryParams with
            | Some "button" -> 
                body [] [
                    text "Hello 3D"
                    br []
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
                    br []
                ]
            | Some "render" -> 
                body [] [
                    //text "oida"
                    renderControl
                ]
            | _ -> 
                require docking (
                    body [] [
                        onBoot dockingBoot (
                            div [clazz "dock-root"; style "width:100%;height:100%;"] []
                        )
                    ]
                )
    )

let threads (model : Model) = 
    CameraController.threads model.cameraState |> ThreadPool.map Camera

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
            }
        update = update 
        view = view
    }
