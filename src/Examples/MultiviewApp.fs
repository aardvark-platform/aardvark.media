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
        (AttributeMap.ofList [ attribute "style" "width:85%; height: 50%; float: left;"]) (viewScene m)

let switchCode = """
    debugger;
    var f = function() {
    if(window.location.href.indexOf('complex') !== -1)
    {   
        debugger;
        var blub = $( ".simple" ).style;
        $( ".simple" )[0].style.display = 'block';
    } else
    {
        debugger;
        $( ".complex" )[0].hide();
    }
    };
    window.addEventListener("load", function load(event) {
        f();
    }, false);
    f();
"""


let view (m : MModel) =
    let complex = complex m
    let simple = simple m
  
    require (Html.semui) (
        page <| fun (request : Request) ->
            match request.requestPath with
                | "simple" :: rest ->
                    let content = 
                        body [ style "background: #1B1C1E"] [
                            require (Html.semui) (
                                div [] [
                                    a [attribute "href" "./complex"; attribute "target" "_blank"] [text "complex view"]
                                    button [
                                        clientEvent "onclick" "aardvark.openFileDialog({ mode: 'file'}, function(path) { aardvark.processEvent('__ID__', 'onselect', path); });"
                                        onEvent "onselect" [] (function files::_ -> SelectFiles(Pickler.unpickleOfJson files) | _ -> SelectFiles [])
                                    ] [text "open file"]
                                    div [clazz "simple"] [simple]
                                ]
                            )
                        ]
                    Some (content, { request with requestPath = rest })
                | "complex" :: rest ->
                    let content = 
                        body [ style "background: #1B1C1E"] [
                            require (Html.semui) (
                                div [] [
                                    div [clazz "complex"] [complex]
                                ]
                            )
                        ]
                    Some (content, { request with requestPath = rest })
                | _ -> 
                    None
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
