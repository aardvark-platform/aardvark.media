module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model


let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | LoadFiles s -> 
            printfn "%A" msg
            model
        | SaveFile s -> 
            printfn "%A" msg
            model

let viewScene (model : AdaptiveModel) =
    Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let onChooseFiles (chosen : list<string> -> 'msg) =
    let cb xs =
        match xs with
            | [] -> chosen []
            | x::[] when x <> null -> x |> Aardvark.Service.Pickler.json.UnPickleOfString |> List.map Aardvark.Service.PathUtils.ofUnixStyle |> chosen
            | _ -> failwithf "onChooseFiles: %A" xs
    onEvent "onchoose" [] cb
        
let onSaveFile (chosen : string -> 'msg) =
    let cb xs =
        match xs with
            | x::[] when x <> null -> x |> Aardvark.Service.Pickler.json.UnPickleOfString |> Aardvark.Service.PathUtils.ofUnixStyle |> chosen
            | _ -> failwithf "onSaveFile: %A" xs
    onEvent "onsave" [] cb



let dependencies = [
    { name = "helper"; url = "resources/helper.js"; kind = Script }
]

let view (model : AdaptiveModel) =

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    (AttributeMap.ofList [ style "width: 50%;height:50%;"; clazz "myRenderControl"]) 
                    (viewScene model)

    require dependencies (
        body [] [
            div [] [
                button [
                    onChooseFiles (fun files -> printfn "%A" files; LoadFiles files)
                    clientEvent "onclick" ("aardvark.processEvent('__ID__', 'onchoose', aardvark.dialog.showOpenDialog({properties: ['openFile', 'openDirectory', 'multiSelections']}));") 
                ] [text "open directory"]
                br []
                button [
                    onSaveFile SaveFile
                    clientEvent "onclick" ("aardvark.processEvent('__ID__', 'onsave', aardvark.dialog.showSaveDialog({properties: []}));") 
                ] [text "save file"]
                renderControl
                button [
                    clientEvent "onclick" "screenit('myRenderControl')"
                ] [text "screenshot"]
                onBoot "installScreenshotPath(__ID__,'myRenderControl')" (
                    input []
                )
            ]
        ]
    )

let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map Camera


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
