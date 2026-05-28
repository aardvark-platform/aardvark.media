module App

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

open RenderingParametersModel
open Model

type Action = MultiselectPropertiesAction

let layoutConfig = LayoutConfig.Default

let defaultLayout =
    layout {
        row {
            element {
                id "render"
                title "3D View"
                minSize 100
                weight 65
            }
            column {
                weight 35
                element {
                    id "list"
                    title "Box List"
                }
                element {
                    id "controls"
                    title "Controls"
                }
            }
        }
    }

let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox = 
    {
        id = Guid.NewGuid().ToString()
        geometry = box
        color = color           
    }

let update (model : MultiselectPropertiesModel) (act : Action) =
        
    match act with
    | CameraMessage m -> 
        { model with camera = FreeFlyController.update model.camera m }          
    | RenderingAction a ->
        { model with rendering = RenderingParameters.update model.rendering a }
    | Select id -> 
        let selection = 
            if HashSet.contains id model.selectedBoxes 
            then HashSet.remove id model.selectedBoxes 
            else HashSet.add id model.selectedBoxes

        { model with selectedBoxes = selection }           
    | Enter id -> { model with boxHovered = Some id }            
    | Exit -> { model with boxHovered = None }                             
    | AddBox -> 
        let i = model.boxes.Count                
        let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox Primitives.colors.[i % 5]
                                     
        { model with boxes = IndexList.add box model.boxes }
    | RemoveBox ->  
        let i = model.boxes.Count - 1
        let boxes = IndexList.removeAt i model.boxes

        { model with boxes = boxes }
    | ClearSelection -> { model with selectedBoxes = HashSet.empty }
    | GoldenLayoutMsg msg -> { model with golden = model.golden |> GoldenLayout.update msg }
                    
let mkColor (model : AdaptiveMultiselectPropertiesModel) (box : AdaptiveVisibleBox) =
    let id = box.id 

    let color =  
        model.selectedBoxes 
            |> ASet.toAVal 
            |> AVal.map (HashSet.contains id) 
            |> AVal.bind (function 
                | true -> AVal.constant Primitives.selectionColor 
                | false -> box.color
              )

    let color = 
        model.boxHovered |> AVal.bind (function 
            | Some k -> if k = id then AVal.constant Primitives.hoverColor else color
            | None -> color
        )

    color

let mkISg (model : AdaptiveMultiselectPropertiesModel) (box : AdaptiveVisibleBox) =
                
    let color = mkColor model box

    Sg.box color box.geometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
            }                
        |> Sg.requirePicking
        |> Sg.noEvents
        |> Sg.fillMode model.rendering.fillMode
        |> Sg.cullMode model.rendering.cullMode
        |> Sg.withEvents [
            Sg.onClick (fun _ -> Select box.id)
            Sg.onEnter (fun _ -> Enter box.id)
            Sg.onLeave (fun () -> Exit)
        ]

let view (model : AdaptiveMultiselectPropertiesModel) =
    let frustum = AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

    pages (function
        | Pages.Page "render" ->
            FreeFlyController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                    style "width: 100%; height: 100%; background: #1B1C1E"
                    attribute "data-samples" "8"
                ])
                (
                    model.boxes
                        |> AList.toASet
                        |> ASet.map (fun b -> mkISg model b)
                        |> Sg.set
                        |> Sg.effect [
                            toEffect DefaultSurfaces.trafo
                            toEffect DefaultSurfaces.vertexColor
                            toEffect DefaultSurfaces.simpleLighting
                        ]
                        |> Sg.noEvents
                )

        | Pages.Page "list" ->
            require (Html.semui) (
                Incremental.div
                    (AttributeMap.ofList [clazz "ui divided list"; style "background: #1B1C1E; height: 100%; overflow-y: auto"]) (
                        alist {
                            for b in model.boxes do
                                let! c = mkColor model b
                                let bgc = sprintf "background: %s" (Html.color c)
                                yield
                                    div [
                                        clazz "item"; style bgc
                                        onClick (fun _ -> Select b.id)
                                        onMouseEnter (fun _ -> Enter b.id)
                                        onMouseLeave (fun _ -> Exit)
                                    ] [
                                        i [clazz "file outline middle aligned icon"] []
                                        text b.id
                                    ]
                        }
                    )
            )

        | Pages.Page "controls" ->
            require (Html.semui) (
                div [style "background: #1B1C1E; height: 100%; overflow-y: auto"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingParameters.view model.rendering |> UI.map RenderingAction
                    ]
                    div [clazz "ui buttons"; style "padding: 4px"] [
                        button [clazz "ui button"; onMouseClick (fun _ -> AddBox)] [text "Add Box"]
                        button [clazz "ui button"; onMouseClick (fun _ -> RemoveBox)] [text "Remove Box"]
                        button [clazz "ui button"; onMouseClick (fun _ -> ClearSelection)] [text "Clear Selection"]
                    ]
                ]
            )

        | Pages.Page unknown ->
            div [style "color: red; padding: 10px"] [
                text (sprintf "Unknown panel: %s" unknown)
            ]

        | Pages.Body ->
            body [style "width: 100%; height: 100%; overflow: hidden; margin: 0"] [
                GoldenLayout.view
                    [ style "width: 100%; height: 100%; min-width: 400px; min-height: 400px; overflow: hidden" ]
                    model.golden
            ]
    )
             
let initial =
    {
        camera           = FreeFlyController.initial            
        rendering        = RenderingParameters.initial            
        boxHovered       = None
        boxes = Primitives.mkBoxes 3 |> List.mapi (fun i k -> mkVisibleBox Primitives.colors.[i % 5] k) |> IndexList.ofList
        selectedBoxes = HashSet.empty     
        boxesMap = HashMap.empty
        golden = GoldenLayout.create layoutConfig defaultLayout
    }

let app : App<MultiselectPropertiesModel, AdaptiveMultiselectPropertiesModel, Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }
