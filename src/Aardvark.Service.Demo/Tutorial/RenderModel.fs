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
    | LoadModel of string
    | CameraAction of CameraController.Message

let update (m : Model) (a : Action) =
    match a with
        | SetObject object -> { m with currentModel = Some object }
        | CameraAction a ->   { m with cameraState = CameraController.update m.cameraState a }
        | LoadModel file ->   { m with currentModel = Some (FileModel file) }


let renderModel (model : IMod<MObject>) =
    adaptive {
        let! currentModel = model // type could change, read adaptively
        match currentModel with
            | MFileModel fileName -> 
                let! file = fileName // read current filename
                return Sg.Assimp.loadFromFile file |> Sg.normalize // create scenegraph
            | MSphereModel(center,radius) ->
                let sphere = Sg.sphere 3 (Mod.constant C4b.White) radius 
                // create unit sphere of given mod radius and translate adaptively
                return Sg.translate' center sphere
            | MBoxModel b -> 
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
                do! DefaultSurfaces.constantColor C4f.White
                do! DefaultSurfaces.simpleLighting
           }
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant
    let attributes = AttributeMap.ofList [ attribute "style" "width:100%; height: 100%"]
    CameraController.controlledControl m.cameraState CameraAction frustum attributes sg

let eigi = FileModel @"C:\Development\aardvark.rendering\data\eigi\eigi.dae"
let defaultSphere = SphereModel(V3d.OOO,1.0)
let defaultBox = BoxModel Box3d.Unit

let view (m : MModel) =
    require Html.semui (
        div [] (
            Html.SemUi.adornerMenu [ 
                "Set Scene", [ 
                    button [clazz "ui icon button"; onClick (fun _ -> SetObject eigi)] [text "The famous eigi model"]
                    button [clazz "ui icon button"; onClick (fun _ -> SetObject defaultSphere)] [text "Sphere"] 
                    button [clazz "ui icon button"; onClick (fun _ -> SetObject defaultBox)] [text "Box"] 
                    button [ 
                             clazz "ui button";
                             onEvent "onchoose" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> LoadModel)
                             clientEvent "onclick" ("aardvark.openFileDialog({ allowMultiple: true, mode: 'file' }, function(files) { if(files != undefined) aardvark.processEvent('__ID__', 'onchoose', files); });")
                           ] [ text "Load from File"]
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