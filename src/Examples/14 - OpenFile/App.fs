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
        | OpenFile m -> 
            { model with currentFile = m }

let viewScene (model : MModel) =
    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

let view (model : MModel) =
    body [] [
        button [
            onEvent "onchoose" [] (List.head >> Aardvark.Service.Pickler.json.UnPickleOfString >> OpenFile)
            clientEvent "onclick" ("aardvark.openFileDialog({ allowMultiple: true, mode: 'file' }, function(files) { if(files != undefined) aardvark.processEvent('__ID__', 'onchoose', files); });")
        ] [text "Open File"]
        br []
        Incremental.text model.currentFile
    ]

let threads (model : Model) = 
    ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               currentFile = "hugo.txt"
            }
        update = update 
        view = view
    }
