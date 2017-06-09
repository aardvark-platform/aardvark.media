module RenderModelApp

open RenderModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.IO


open Aardvark.UI
open Aardvark.UI.Primitives

type Action = 
    | SetObject of Object 
    | CameraAction of CameraController.Message

let update (m : Model) (a : Action) =
    match a with
        | SetObject object -> { m with currentModel = Some object }
        | CameraAction a -> { m with cameraState = CameraController.update m.cameraState a }


let renderModel (model : IMod<MObject>) =
    adaptive {
        let! currentModel = model // type could change, read adaptively
        match currentModel with
            | MFileModel fileName -> 
                let! file = fileName // read current filename
                return Sg.Assimp.loadFromFile file // create scenegraph
            | MSphere(center,radius) ->
                let sphere = Sg.sphere 3 (Mod.constant C4b.White) radius 
                // create unit sphere of given mod radius and translate adaptively
                return Sg.translate' center sphere
            | MBox b -> 
                return Sg.box (Mod.constant C4b.White) b //adaptively create box
    }

let view3D (m : MModel) =

    let model =
        adaptive {
            let! model = m.currentModel
            match model with
                | None -> 
                    return Sg.empty // no model specified, render nothing
                | Some model -> 
                    return! renderModel model // render the model
        }

    let sg : ISg<Action> =
        model
        |> Sg.dynamic
        |> Sg.trafo m.trafo
        |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
           }
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant
    let attributes = AttributeMap.ofList [ attribute "style" "width:100%; height: 100%"]
    CameraController.controlledControl m.cameraState CameraAction frustum attributes sg

let view (m : MModel) =
    require Html.semui (
        div [] (
            Html.SemUi.adornerMenu [ 
                "Set Scene", [ 
                    button [clazz "ui icon button"] [text "Eigi"] 
                    button [clazz "ui icon button"] [text "Sphere"] 
                    button [clazz "ui icon button"] [text "Box"] 
                    button [clazz "ui icon button"] [text "File"] 
                ] 
            ] [view3D m]
        )
    )


let app =
    {
        unpersist = Unpersist.instance
        threads = fun (model : Model) -> CameraController.threads model.cameraState |> ThreadPool.map CameraAction
        initial = { 
                    currentModel = None; 
                    cameraState = CameraController.initial; 
                    trafo = Trafo3d.Identity 
                  }
        update = update
        view = view
    }

let start() = App.start app