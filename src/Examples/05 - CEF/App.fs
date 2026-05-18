module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model

let update (model : Model) (msg : Message) =
    match msg with
    | Inc -> { model with value = model.value + 1 }
    | ChooseFiles files -> Log.line "Files: %A" files; model
    | ChooseFolder folder -> Log.line "Folder: %s" folder; model

let view (model : AdaptiveModel) =
    body [] [
        text "Hello World"
        br []
        button [onClick (fun _ -> Inc)] [text "Increment"]
        text "    "
        Incremental.text (model.value |> AVal.map string)
        br []
        openDialogButton { OpenDialogConfig.file with title = "YOOO"; allowMultiple = true; filters = [|"*.fs"|] } [onChooseFiles ChooseFiles] [text "Open files"]
        br []
        openDialogButton { OpenDialogConfig.folder with title = "Heyy"} [onChooseFile ChooseFolder] [text "Open folder"]
        br []
        img [
            attribute "src" "https://upload.wikimedia.org/wikipedia/commons/6/67/SanWild17.jpg"; 
            attribute "alt" "aardvark"
            style "width: 80%"
        ]
    ]

let threads (model : Model) = 
    ThreadPool.empty


let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               value = 0
            }
        update = update 
        view = view
    }
