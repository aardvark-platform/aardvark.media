module Examples.MultiviewApp

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Examples.MultiviewModel

let update (m : Model) (message : Message) =
    match message with
        | CameraMessage1 msg -> { m with camera1 = CameraController.update m.camera1 msg }
        | CameraMessage2 msg -> { m with camera2 = CameraController.update m.camera2 msg }
        | CameraMessage3 msg -> { m with camera3 = CameraController.update m.camera3 msg }
        | SelectFiles files -> 
            let files = files |> List.map Aardvark.Service.PathUtils.ofUnixStyle
            Log.warn "%A" files; m

let viewScene (m : MModel) =
    Sg.box (Mod.constant C4b.Red) (Mod.constant Box3d.Unit)
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }

let complex (m : MModel) =
    div [clazz "ui"; style "background: #1B1C1E"] [     
        CameraController.controlledControl m.camera1 CameraMessage1 
            (Frustum.perspective 40.0 0.1 100.0 1.0 |> Mod.constant) 
            (AttributeMap.ofList [ attribute "style" "width:85%; height: 50%; float: left;"]) (viewScene m)
                
        br []
        CameraController.controlledControl m.camera2 CameraMessage2 
            (Frustum.perspective 80.0 0.1 100.0 1.0 |> Mod.constant) 
            (AttributeMap.ofList [ attribute "style" "width:85%; height: 50%; float: left;"]) (viewScene m)
        br []

        div [style "width:15%; height: 100%; float:right"] [
            Html.SemUi.stuffStack [
                button [clazz "ui button"; ] [text "Hello World"]
                br []

            ]
        ]
    ]

let simple (m : MModel) =
    CameraController.controlledControl m.camera3 CameraMessage3 
        (Frustum.perspective 80.0 0.1 100.0 1.0 |> Mod.constant) 
        (AttributeMap.ofList [ attribute "data-scene" "myscene"; attribute "style" "width:85%; height: 50%; float: left;"]) (viewScene m)

let browseOnClick (react : list<string> -> Option<'msg>) : Attribute<'msg> =
    "onclick", 
    AttributeValue.Event { 
        clientSide = fun send id -> 
            "aardvark.openFileDialog({ mode: 'file'}, function(path) { " + send id ["path"] + " });"
        serverSide = 
            fun _ _ files -> 
                match files with
                    | files::_ ->
                        Pickler.unpickleOfJson files 
                            |> List.map Aardvark.Service.PathUtils.ofUnixStyle 
                            |> react 
                            |> Option.toList :> seq<_>
                    | _ ->
                        Seq.empty
    }

let view (m : MModel) =
    let complex = complex m
    let simple = simple m

  
    require (Html.semui) (
        page <| fun (request : Request) ->
            match Map.tryFind "viewType" request.queryParams with
                | Some "complex" ->
                    body [ style "background: #1B1C1E"] [
                        require (Html.semui) (
                            div [] [
                                div [clazz "complex"] [complex]
                            ]
                        )
                    ]
                | _ ->
                    body [ style "background: #1B1C1E"] [
                        require (Html.semui) (
                            div [] [
                                a [attribute "href" "./?viewType=complex"; attribute "target" "_blank"] [text "complex view"]
                                button [ browseOnClick (fun files -> Some (SelectFiles files)) ] [text "open file"]
                                div [clazz "simple"] [simple]
                            ]
                        )
                    ]
    )
    

let threads (m : Model) =
    ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
                camera1 = CameraController.initial 
                camera2 = CameraController.initial 
                camera3 = CameraController.initial
            }
        update = update 
        view = view
    }
